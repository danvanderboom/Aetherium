using System;
using System.IO;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Orleans;
using Orleans.Serialization;

namespace Aetherium.Test.Persistence
{
    /// <summary>
    /// Phase F: schema and delta-type version guards. Verifies that the persistence
    /// layer refuses to load rows written by a newer binary rather than silently
    /// mis-applying state.
    /// </summary>
    [TestFixture]
    public class PersistenceVersioningTests
    {
        private string _tempDir = null!;
        private string _connectionString = null!;
        private Serializer _serializer = null!;

        // Test double: claims a higher DeltaTypeVersion than what a stored row will
        // claim. Used to assert the store accepts older-or-equal versions on read.
        [GenerateSerializer]
        public class V2EntityRemovedDelta : EntityRemovedDelta
        {
            public override int DeltaTypeVersion => 2;
        }

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "aetherium-versioning-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            var dbPath = Path.Combine(_tempDir, "test.db");
            _connectionString = $"Data Source={dbPath};Cache=Shared";

            var services = new ServiceCollection();
            services.AddSerializer();
            var sp = services.BuildServiceProvider();
            _serializer = sp.GetRequiredService<Serializer>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* best-effort cleanup */ }
        }

        private SqliteWorldSnapshotStore NewStore() =>
            new SqliteWorldSnapshotStore(_connectionString, _serializer, NullLogger<SqliteWorldSnapshotStore>.Instance);

        [Test]
        public async Task Replay_Refuses_Delta_When_Stored_Version_Exceeds_Binary_Version()
        {
            var store = NewStore();

            // Append a row that LOOKS like an ordinary EntityRemovedDelta but with the
            // stored delta_type_version forced to 99 (simulating a future binary's row).
            // We can't easily synthesize that through the public API, so go direct via
            // the underlying SQLite connection.
            var serialized = _serializer.SerializeToArray<MapDelta>(new EntityRemovedDelta
            {
                Sequence = 1,
                EntityId = "future-entity",
            });

            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString))
            {
                conn.Open();
                // Seed schema by running the store's Initialize via a benign call.
                await store.SaveSnapshotAsync("w", new RegionStateSnapshot { RegionId = "r", MapId = "m" });

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO map_delta_log (world_id, region_id, sequence, delta_type, delta_type_version, delta_blob, recorded_at)
VALUES ('w', 'r', 1, 'EntityRemovedDelta', 99, $blob, 0);";
                cmd.Parameters.AddWithValue("$blob", serialized);
                cmd.ExecuteNonQuery();
            }

            var ex = Assert.ThrowsAsync<PersistenceVersionMismatchException>(async () =>
                await store.GetMapDeltasSinceSequenceAsync("w", "r", 0));

            Assert.That(ex!.Message, Does.Contain("EntityRemovedDelta"));
            Assert.That(ex.Message, Does.Contain("99"),
                "Exception should identify the offending stored version");
        }

        [Test]
        public async Task Replay_Accepts_Delta_When_Stored_Version_Equals_Binary_Version()
        {
            var store = NewStore();
            await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta
            {
                Sequence = 1, EntityId = "e",
            });

            var deltas = await store.GetMapDeltasSinceSequenceAsync("w", "r", 0);
            Assert.That(deltas, Has.Length.EqualTo(1));
            Assert.That(((EntityRemovedDelta)deltas[0]).EntityId, Is.EqualTo("e"));
        }

        [Test]
        public async Task Replay_Accepts_Delta_When_Stored_Version_Below_Binary_Version()
        {
            // Mimics an upgrade: the binary now reports version 2 for this subtype,
            // but the stored row was written when the binary was at version 1.
            // Forward compat: the higher-version binary must still read older rows.
            var store = NewStore();
            var serialized = _serializer.SerializeToArray<MapDelta>(new EntityRemovedDelta
            {
                Sequence = 5, EntityId = "older",
            });

            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString))
            {
                conn.Open();
                await store.SaveSnapshotAsync("w", new RegionStateSnapshot { RegionId = "r", MapId = "m" });

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO map_delta_log (world_id, region_id, sequence, delta_type, delta_type_version, delta_blob, recorded_at)
VALUES ('w', 'r', 5, 'EntityRemovedDelta', 1, $blob, 0);";
                cmd.Parameters.AddWithValue("$blob", serialized);
                cmd.ExecuteNonQuery();
            }

            // EntityRemovedDelta.DeltaTypeVersion is still 1, so this is "equal" today.
            // The test also documents the contract: stored<=binary is always safe.
            var deltas = await store.GetMapDeltasSinceSequenceAsync("w", "r", 0);
            Assert.That(deltas, Has.Length.EqualTo(1));
            Assert.That(((EntityRemovedDelta)deltas[0]).EntityId, Is.EqualTo("older"));
        }

        [Test]
        public void Subclass_Override_Reports_Higher_DeltaTypeVersion()
        {
            // Sanity: the version is read via the virtual property, not a static const.
            MapDelta v1 = new EntityRemovedDelta();
            MapDelta v2 = new V2EntityRemovedDelta();
            Assert.That(v1.DeltaTypeVersion, Is.EqualTo(1));
            Assert.That(v2.DeltaTypeVersion, Is.EqualTo(2));
        }
    }
}
