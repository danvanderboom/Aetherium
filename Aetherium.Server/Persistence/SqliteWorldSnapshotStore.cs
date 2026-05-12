using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Orleans.Serialization;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IWorldSnapshotStore"/>. Persists region
    /// snapshots, the legacy <see cref="RegionDelta"/> log, and the production
    /// <see cref="MapDelta"/> sequence-keyed log in a single SQLite database (shared with
    /// <see cref="SqliteGrainStorage"/>). All blobs are serialized through Orleans's
    /// binary serializer so <c>[GenerateSerializer]</c>-annotated types round-trip without
    /// extra configuration.
    /// </summary>
    public sealed class SqliteWorldSnapshotStore : IWorldSnapshotStore
    {
        private readonly string _connectionString;
        private readonly Serializer _serializer;
        private readonly ILogger<SqliteWorldSnapshotStore> _logger;
        private int _initialized;

        public SqliteWorldSnapshotStore(string connectionString, Serializer serializer, ILogger<SqliteWorldSnapshotStore> logger)
        {
            _connectionString = connectionString;
            _serializer = serializer;
            _logger = logger;
        }

        private void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

            EnsureDirectoryExists();

            using var conn = OpenConnection();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();

            using var create = conn.CreateCommand();
            create.CommandText = @"
CREATE TABLE IF NOT EXISTS region_snapshots (
    world_id      TEXT NOT NULL,
    region_id     TEXT NOT NULL,
    snapshot_blob BLOB NOT NULL,
    saved_at      INTEGER NOT NULL,
    PRIMARY KEY (world_id, region_id)
);
CREATE TABLE IF NOT EXISTS region_delta_log (
    world_id   TEXT NOT NULL,
    region_id  TEXT NOT NULL,
    timestamp  INTEGER NOT NULL,
    delta_blob BLOB NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_region_delta_log_lookup
    ON region_delta_log(world_id, region_id, timestamp);
CREATE TABLE IF NOT EXISTS map_delta_log (
    world_id           TEXT NOT NULL,
    region_id          TEXT NOT NULL,
    sequence           INTEGER NOT NULL,
    delta_type         TEXT NOT NULL,
    delta_type_version INTEGER NOT NULL DEFAULT 1,
    delta_blob         BLOB NOT NULL,
    recorded_at        INTEGER NOT NULL,
    PRIMARY KEY (world_id, region_id, sequence)
);";
            create.ExecuteNonQuery();

            // Best-effort upgrade for databases created before Phase F. SQLite raises a
            // "duplicate column name" error when the column already exists — safe to ignore.
            try
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE map_delta_log ADD COLUMN delta_type_version INTEGER NOT NULL DEFAULT 1;";
                alter.ExecuteNonQuery();
            }
            catch (Microsoft.Data.Sqlite.SqliteException) { /* already present */ }

            _logger.LogInformation("SqliteWorldSnapshotStore initialized at {ConnectionString}", _connectionString);
        }

        private void EnsureDirectoryExists()
        {
            var csb = new SqliteConnectionStringBuilder(_connectionString);
            var path = csb.DataSource;
            if (string.IsNullOrEmpty(path) || path == ":memory:") return;
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot)
        {
            Initialize();
            var bytes = _serializer.SerializeToArray(snapshot);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO region_snapshots (world_id, region_id, snapshot_blob, saved_at)
VALUES ($w, $r, $b, $t)
ON CONFLICT (world_id, region_id) DO UPDATE SET
    snapshot_blob = excluded.snapshot_blob,
    saved_at      = excluded.saved_at;";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", snapshot.RegionId);
            cmd.Parameters.AddWithValue("$b", bytes);
            cmd.Parameters.AddWithValue("$t", now);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        public Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT snapshot_blob FROM region_snapshots
WHERE world_id = $w AND region_id = $r;";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return Task.FromResult<RegionStateSnapshot?>(null);
            var bytes = (byte[])reader["snapshot_blob"];
            var snapshot = _serializer.Deserialize<RegionStateSnapshot>(bytes);
            return Task.FromResult<RegionStateSnapshot?>(snapshot);
        }

        public Task DeleteSnapshotAsync(string worldId, string regionId)
        {
            Initialize();
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();
            foreach (var sql in new[]
            {
                "DELETE FROM region_snapshots WHERE world_id = $w AND region_id = $r;",
                "DELETE FROM region_delta_log WHERE world_id = $w AND region_id = $r;",
                "DELETE FROM map_delta_log    WHERE world_id = $w AND region_id = $r;",
            })
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("$w", worldId);
                cmd.Parameters.AddWithValue("$r", regionId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return Task.CompletedTask;
        }

        public Task<string[]> ListRegionIdsAsync(string worldId)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT region_id FROM region_snapshots WHERE world_id = $w;";
            cmd.Parameters.AddWithValue("$w", worldId);
            using var reader = cmd.ExecuteReader();
            var result = new List<string>();
            while (reader.Read())
            {
                result.Add((string)reader["region_id"]);
            }
            return Task.FromResult(result.ToArray());
        }

        public Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta)
        {
            Initialize();
            var bytes = _serializer.SerializeToArray(delta);
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO region_delta_log (world_id, region_id, timestamp, delta_blob)
VALUES ($w, $r, $t, $b);";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            cmd.Parameters.AddWithValue("$t", new DateTimeOffset(delta.Timestamp.ToUniversalTime()).ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$b", bytes);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        public Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT delta_blob FROM region_delta_log
WHERE world_id = $w AND region_id = $r AND timestamp >= $since
ORDER BY timestamp ASC;";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            cmd.Parameters.AddWithValue("$since", new DateTimeOffset(since.ToUniversalTime()).ToUnixTimeMilliseconds());
            using var reader = cmd.ExecuteReader();
            var result = new List<RegionDelta>();
            while (reader.Read())
            {
                var bytes = (byte[])reader["delta_blob"];
                result.Add(_serializer.Deserialize<RegionDelta>(bytes));
            }
            return Task.FromResult(result.ToArray());
        }

        public Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot)
        {
            Initialize();
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            // Persist the snapshot (acts as the new baseline).
            var snapshotBytes = _serializer.SerializeToArray(baseSnapshot);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using (var saveCmd = conn.CreateCommand())
            {
                saveCmd.Transaction = tx;
                saveCmd.CommandText = @"
INSERT INTO region_snapshots (world_id, region_id, snapshot_blob, saved_at)
VALUES ($w, $r, $b, $t)
ON CONFLICT (world_id, region_id) DO UPDATE SET
    snapshot_blob = excluded.snapshot_blob,
    saved_at      = excluded.saved_at;";
                saveCmd.Parameters.AddWithValue("$w", worldId);
                saveCmd.Parameters.AddWithValue("$r", baseSnapshot.RegionId);
                saveCmd.Parameters.AddWithValue("$b", snapshotBytes);
                saveCmd.Parameters.AddWithValue("$t", now);
                saveCmd.ExecuteNonQuery();
            }

            using (var clearCmd = conn.CreateCommand())
            {
                clearCmd.Transaction = tx;
                clearCmd.CommandText = "DELETE FROM region_delta_log WHERE world_id = $w AND region_id = $r;";
                clearCmd.Parameters.AddWithValue("$w", worldId);
                clearCmd.Parameters.AddWithValue("$r", regionId);
                clearCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return Task.CompletedTask;
        }

        public Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta)
        {
            Initialize();
            var bytes = _serializer.SerializeToArray(delta);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO map_delta_log (world_id, region_id, sequence, delta_type, delta_type_version, delta_blob, recorded_at)
VALUES ($w, $r, $s, $dt, $dv, $b, $t);";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            cmd.Parameters.AddWithValue("$s", delta.Sequence);
            cmd.Parameters.AddWithValue("$dt", delta.GetType().Name);
            cmd.Parameters.AddWithValue("$dv", delta.DeltaTypeVersion);
            cmd.Parameters.AddWithValue("$b", bytes);
            cmd.Parameters.AddWithValue("$t", now);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        public Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT delta_type, delta_type_version, delta_blob FROM map_delta_log
WHERE world_id = $w AND region_id = $r AND sequence > $s
ORDER BY sequence ASC;";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            cmd.Parameters.AddWithValue("$s", sinceSequence);
            using var reader = cmd.ExecuteReader();
            var result = new List<MapDelta>();
            while (reader.Read())
            {
                var storedVersion = Convert.ToInt32(reader["delta_type_version"]);
                var deltaTypeName = (string)reader["delta_type"];
                var bytes = (byte[])reader["delta_blob"];
                var delta = _serializer.Deserialize<MapDelta>(bytes);

                // Version guard: if the stored row was written by a newer binary that
                // understands a higher version of this delta subtype, refuse to load
                // rather than silently mis-applying. Old binaries' deltas (storedVersion
                // less than or equal to current) are always readable thanks to Orleans's
                // [Id] forward-compatibility.
                if (storedVersion > delta.DeltaTypeVersion)
                {
                    throw new PersistenceVersionMismatchException(
                        $"Cannot replay map delta seq={delta.Sequence} type={deltaTypeName}: " +
                        $"stored version {storedVersion} exceeds this binary's supported version {delta.DeltaTypeVersion}. " +
                        "Upgrade the server binary or restore from a compatible snapshot.");
                }
                result.Add(delta);
            }
            return Task.FromResult(result.ToArray());
        }

        public Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM map_delta_log
WHERE world_id = $w AND region_id = $r AND sequence <= $s;";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$r", regionId);
            cmd.Parameters.AddWithValue("$s", throughSequence);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        public Task DeleteWorldAsync(string worldId)
        {
            Initialize();
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();
            foreach (var sql in new[]
            {
                "DELETE FROM region_snapshots WHERE world_id = $w;",
                "DELETE FROM region_delta_log  WHERE world_id = $w;",
                "DELETE FROM map_delta_log     WHERE world_id = $w;",
            })
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("$w", worldId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return Task.CompletedTask;
        }
    }
}
