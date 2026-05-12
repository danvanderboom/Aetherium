using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Storage;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IGrainStorage"/>.
    /// One row per (storage_name, grain_type, grain_id). State is serialized via Orleans's
    /// binary serializer so types annotated with [GenerateSerializer] round-trip without
    /// extra configuration. ETag is a fresh Guid on every write for optimistic concurrency.
    /// </summary>
    public sealed class SqliteGrainStorage : IGrainStorage
    {
        private readonly string _storageName;
        private readonly string _connectionString;
        private readonly Serializer _serializer;
        private readonly ILogger<SqliteGrainStorage> _logger;
        private int _initialized;

        public SqliteGrainStorage(string storageName, string connectionString, Serializer serializer, ILogger<SqliteGrainStorage> logger)
        {
            _storageName = storageName;
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
CREATE TABLE IF NOT EXISTS grain_state (
    storage_name TEXT NOT NULL,
    grain_type   TEXT NOT NULL,
    grain_id     TEXT NOT NULL,
    state_blob   BLOB NOT NULL,
    etag         TEXT NOT NULL,
    written_at   INTEGER NOT NULL,
    PRIMARY KEY (storage_name, grain_type, grain_id)
);";
            create.ExecuteNonQuery();

            _logger.LogInformation("SqliteGrainStorage '{Name}' initialized at {ConnectionString}", _storageName, _connectionString);
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

        public Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT state_blob, etag FROM grain_state
WHERE storage_name = $s AND grain_type = $t AND grain_id = $g;";
            cmd.Parameters.AddWithValue("$s", _storageName);
            cmd.Parameters.AddWithValue("$t", stateName);
            cmd.Parameters.AddWithValue("$g", grainId.ToString());

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var bytes = (byte[])reader["state_blob"];
                var etag = (string)reader["etag"];
                grainState.State = _serializer.Deserialize<T>(bytes);
                grainState.ETag = etag;
                grainState.RecordExists = true;
            }
            else
            {
                grainState.State = Activator.CreateInstance<T>();
                grainState.ETag = string.Empty;
                grainState.RecordExists = false;
            }
            return Task.CompletedTask;
        }

        public Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            Initialize();
            var bytes = _serializer.SerializeToArray(grainState.State);
            var newEtag = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            // Optimistic concurrency: if the row exists, the stored etag must match.
            // If RecordExists is false the caller asserts no prior row; reject if one snuck in.
            using (var check = conn.CreateCommand())
            {
                check.Transaction = tx;
                check.CommandText = @"SELECT etag FROM grain_state
WHERE storage_name = $s AND grain_type = $t AND grain_id = $g;";
                check.Parameters.AddWithValue("$s", _storageName);
                check.Parameters.AddWithValue("$t", stateName);
                check.Parameters.AddWithValue("$g", grainId.ToString());
                var existingEtag = check.ExecuteScalar() as string;

                if (grainState.RecordExists && existingEtag != null && existingEtag != grainState.ETag)
                {
                    throw new InconsistentStateException(
                        $"ETag mismatch for {stateName}/{grainId} in {_storageName}. Expected {grainState.ETag}, found {existingEtag}.");
                }
                if (!grainState.RecordExists && existingEtag != null)
                {
                    throw new InconsistentStateException(
                        $"Row already exists for {stateName}/{grainId} in {_storageName} but grain state was marked new.");
                }
            }

            using (var upsert = conn.CreateCommand())
            {
                upsert.Transaction = tx;
                upsert.CommandText = @"
INSERT INTO grain_state (storage_name, grain_type, grain_id, state_blob, etag, written_at)
VALUES ($s, $t, $g, $b, $e, $w)
ON CONFLICT (storage_name, grain_type, grain_id) DO UPDATE SET
    state_blob = excluded.state_blob,
    etag       = excluded.etag,
    written_at = excluded.written_at;";
                upsert.Parameters.AddWithValue("$s", _storageName);
                upsert.Parameters.AddWithValue("$t", stateName);
                upsert.Parameters.AddWithValue("$g", grainId.ToString());
                upsert.Parameters.AddWithValue("$b", bytes);
                upsert.Parameters.AddWithValue("$e", newEtag);
                upsert.Parameters.AddWithValue("$w", now);
                upsert.ExecuteNonQuery();
            }

            tx.Commit();

            grainState.ETag = newEtag;
            grainState.RecordExists = true;
            return Task.CompletedTask;
        }

        public Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM grain_state
WHERE storage_name = $s AND grain_type = $t AND grain_id = $g;";
            cmd.Parameters.AddWithValue("$s", _storageName);
            cmd.Parameters.AddWithValue("$t", stateName);
            cmd.Parameters.AddWithValue("$g", grainId.ToString());
            cmd.ExecuteNonQuery();

            grainState.State = Activator.CreateInstance<T>();
            grainState.ETag = string.Empty;
            grainState.RecordExists = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes all grain state rows whose Orleans key is <paramref name="worldId"/> exactly
        /// (WorldGrain, WorldAclGrain, WorldInviteGrain) or starts with
        /// <c>{worldId}:</c> (map grains whose keys follow the <c>{worldId}:map:{guid}</c>
        /// convention set by <c>WorldGrain.AddMapAsync</c>). Call this after
        /// <see cref="IWorldSnapshotStore.DeleteWorldAsync"/> to fully decommission a world.
        /// </summary>
        public Task DeleteWorldGrainStateAsync(string worldId)
        {
            Initialize();
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            // Orleans encodes the string key as the last segment after '/' in GrainId.ToString().
            // Two patterns cover all world-scoped grains:
            //   '%/{worldId}'   — grains whose key IS the worldId (World, Acl, Invite grains)
            //   '%/{worldId}:%' — grains whose key starts with '{worldId}:' (map grains)
            cmd.CommandText = @"
DELETE FROM grain_state
WHERE grain_id LIKE '%/' || $w
   OR grain_id LIKE '%/' || $w || ':%';";
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }
}
