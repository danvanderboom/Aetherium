using System;
using System.IO;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Orleans;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Storage;

namespace Aetherium.Test.Persistence
{
    [TestFixture]
    public class WorldDeletionTests
    {
        private string _tempDir = null!;
        private string _connectionString = null!;
        private Serializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "aetherium-deletion-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            var dbPath = Path.Combine(_tempDir, "test.db");
            _connectionString = $"Data Source={dbPath};Cache=Shared";

            var services = new ServiceCollection();
            services.AddSerializer();
            _serializer = services.BuildServiceProvider().GetRequiredService<Serializer>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* best-effort */ }
        }

        private SqliteWorldSnapshotStore NewSnapshotStore() =>
            new SqliteWorldSnapshotStore(_connectionString, _serializer, NullLogger<SqliteWorldSnapshotStore>.Instance);

        private SqliteGrainStorage NewGrainStorage(string name = "test") =>
            new SqliteGrainStorage(name, _connectionString, _serializer, NullLogger<SqliteGrainStorage>.Instance);

        // ── snapshot store deletion ────────────────────────────────────────────

        [Test]
        public async Task DeleteWorldAsync_Removes_All_Tables_For_World()
        {
            var store = NewSnapshotStore();

            await store.SaveSnapshotAsync("world-a", new RegionStateSnapshot { RegionId = "r1", MapId = "m1" });
            await store.AppendDeltaAsync("world-a", "r1", new RegionDelta { Type = DeltaType.TerrainModified, Timestamp = DateTime.UtcNow });
            await store.AppendMapDeltaAsync("world-a", "r1", new EntityRemovedDelta { Sequence = 1, EntityId = "e1" });

            await store.DeleteWorldAsync("world-a");

            Assert.That(await store.LoadSnapshotAsync("world-a", "r1"), Is.Null,
                "snapshot must be gone");
            Assert.That(await store.GetDeltasSinceAsync("world-a", "r1", DateTime.MinValue), Is.Empty,
                "legacy delta log must be gone");
            Assert.That(await store.GetMapDeltasSinceSequenceAsync("world-a", "r1", 0), Is.Empty,
                "map delta log must be gone");
        }

        [Test]
        public async Task DeleteWorldAsync_Does_Not_Touch_Other_World()
        {
            var store = NewSnapshotStore();

            await store.SaveSnapshotAsync("world-a", new RegionStateSnapshot { RegionId = "r1", MapId = "m1" });
            await store.AppendMapDeltaAsync("world-a", "r1", new EntityRemovedDelta { Sequence = 1, EntityId = "ea" });

            await store.SaveSnapshotAsync("world-b", new RegionStateSnapshot { RegionId = "r1", MapId = "m1" });
            await store.AppendMapDeltaAsync("world-b", "r1", new EntityRemovedDelta { Sequence = 1, EntityId = "eb" });

            await store.DeleteWorldAsync("world-a");

            Assert.That(await store.LoadSnapshotAsync("world-b", "r1"), Is.Not.Null,
                "world-b snapshot must survive");
            var remaining = await store.GetMapDeltasSinceSequenceAsync("world-b", "r1", 0);
            Assert.That(remaining, Has.Length.EqualTo(1));
            Assert.That(((EntityRemovedDelta)remaining[0]).EntityId, Is.EqualTo("eb"));
        }

        [Test]
        public async Task DeleteWorldAsync_Idempotent_On_Already_Deleted_World()
        {
            var store = NewSnapshotStore();
            await store.SaveSnapshotAsync("world-x", new RegionStateSnapshot { RegionId = "r1", MapId = "m1" });
            await store.DeleteWorldAsync("world-x");

            // Second call must not throw.
            Assert.DoesNotThrowAsync(async () => await store.DeleteWorldAsync("world-x"));
        }

        [Test]
        public async Task DeleteWorldAsync_ListRegionIds_Returns_Empty_After_Deletion()
        {
            var store = NewSnapshotStore();
            await store.SaveSnapshotAsync("world-a", new RegionStateSnapshot { RegionId = "r1", MapId = "m1" });
            await store.SaveSnapshotAsync("world-a", new RegionStateSnapshot { RegionId = "r2", MapId = "m2" });

            await store.DeleteWorldAsync("world-a");

            Assert.That(await store.ListRegionIdsAsync("world-a"), Is.Empty);
        }

        // ── grain storage deletion ─────────────────────────────────────────────

        [Test]
        public async Task DeleteWorldGrainStateAsync_Removes_World_And_Map_Grain_Rows()
        {
            var storage = NewGrainStorage();

            // Simulate a WorldGrain row (key = worldId) and two map grain rows
            // (key = {worldId}:map:{guid}).
            var worldGrainId = GrainId.Create("WorldGrain", "world-a");
            var mapGrainId1  = GrainId.Create("GameMapGrain", "world-a:map:aaaaaaaa");
            var mapGrainId2  = GrainId.Create("GameMapGrain", "world-a:map:bbbbbbbb");
            var otherWorldId = GrainId.Create("WorldGrain", "world-b");

            var worldState = new TestGrainState { Value = "world-data" };
            var mapState1  = new TestGrainState { Value = "map1-data" };
            var mapState2  = new TestGrainState { Value = "map2-data" };
            var otherState = new TestGrainState { Value = "other-data" };

            await WriteGrainState(storage, "worldState", worldGrainId, worldState);
            await WriteGrainState(storage, "mapState",   mapGrainId1,  mapState1);
            await WriteGrainState(storage, "mapState",   mapGrainId2,  mapState2);
            await WriteGrainState(storage, "worldState", otherWorldId, otherState);

            await storage.DeleteWorldGrainStateAsync("world-a");

            // world-a rows should be gone
            var gs1 = new TestGrainStateHolder<TestGrainState>();
            await storage.ReadStateAsync("worldState", worldGrainId, gs1);
            Assert.That(gs1.RecordExists, Is.False, "world grain state must be deleted");

            var gs2 = new TestGrainStateHolder<TestGrainState>();
            await storage.ReadStateAsync("mapState", mapGrainId1, gs2);
            Assert.That(gs2.RecordExists, Is.False, "map grain 1 state must be deleted");

            var gs3 = new TestGrainStateHolder<TestGrainState>();
            await storage.ReadStateAsync("mapState", mapGrainId2, gs3);
            Assert.That(gs3.RecordExists, Is.False, "map grain 2 state must be deleted");

            // world-b must survive
            var gs4 = new TestGrainStateHolder<TestGrainState>();
            await storage.ReadStateAsync("worldState", otherWorldId, gs4);
            Assert.That(gs4.RecordExists, Is.True, "world-b grain state must survive");
            Assert.That(gs4.State.Value, Is.EqualTo("other-data"));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static async Task WriteGrainState<T>(SqliteGrainStorage storage, string stateName, GrainId grainId, T value)
        {
            var holder = new TestGrainStateHolder<T> { State = value };
            await storage.WriteStateAsync(stateName, grainId, holder);
        }

        [GenerateSerializer]
        public class TestGrainState
        {
            [Id(0)] public string Value { get; set; } = string.Empty;
        }

        private class TestGrainStateHolder<T> : IGrainState<T>
        {
            public T State { get; set; } = default!;
            public string ETag { get; set; } = string.Empty;
            public bool RecordExists { get; set; }
        }
    }
}
