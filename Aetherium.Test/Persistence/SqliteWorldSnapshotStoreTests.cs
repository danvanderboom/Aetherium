using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Orleans.Serialization;

namespace Aetherium.Test.Persistence
{
    [TestFixture]
    public class SqliteWorldSnapshotStoreTests
    {
        private string _tempDir = null!;
        private string _connectionString = null!;
        private Serializer _serializer = null!;
        private ILogger<SqliteWorldSnapshotStore> _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "aetherium-snapshot-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            var dbPath = Path.Combine(_tempDir, "test.db");
            _connectionString = $"Data Source={dbPath};Cache=Shared";

            var services = new ServiceCollection();
            services.AddSerializer();
            var sp = services.BuildServiceProvider();
            _serializer = sp.GetRequiredService<Serializer>();
            _logger = NullLogger<SqliteWorldSnapshotStore>.Instance;
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
            new SqliteWorldSnapshotStore(_connectionString, _serializer, _logger);

        [Test]
        public async Task Snapshot_Save_And_Load_Roundtrip()
        {
            var store = NewStore();
            var snapshot = new RegionStateSnapshot
            {
                RegionId = "region:1,2,0",
                MapId = "map:42",
                RegionX = 1,
                RegionY = 2,
                ZLevel = 0,
                RegionSize = 64,
                GameTimeHours = 7.25,
                TerrainModifications = new Dictionary<string, string> { ["3,4"] = "stone" },
                TraversalHeatmap = new Dictionary<string, int> { ["5,5"] = 8 },
                WeatherType = "rain",
            };

            await store.SaveSnapshotAsync("world-A", snapshot);
            var loaded = await store.LoadSnapshotAsync("world-A", snapshot.RegionId);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.RegionId, Is.EqualTo("region:1,2,0"));
            Assert.That(loaded.GameTimeHours, Is.EqualTo(7.25).Within(0.0001));
            Assert.That(loaded.TerrainModifications["3,4"], Is.EqualTo("stone"));
            Assert.That(loaded.TraversalHeatmap["5,5"], Is.EqualTo(8));
            Assert.That(loaded.WeatherType, Is.EqualTo("rain"));
        }

        [Test]
        public async Task Snapshot_Survives_Store_Instances()
        {
            var snapshot = new RegionStateSnapshot { RegionId = "r", MapId = "m" };
            {
                var s = NewStore();
                await s.SaveSnapshotAsync("w", snapshot);
            }
            {
                var s2 = NewStore();
                var loaded = await s2.LoadSnapshotAsync("w", "r");
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.RegionId, Is.EqualTo("r"));
            }
        }

        [Test]
        public async Task Map_Delta_Append_And_Range_Query_By_Sequence()
        {
            var store = NewStore();
            for (long i = 1; i <= 5; i++)
            {
                await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta
                {
                    Sequence = i,
                    MapId = "m",
                    EntityId = $"e{i}",
                });
            }

            var since3 = await store.GetMapDeltasSinceSequenceAsync("w", "r", 3);
            Assert.That(since3, Has.Length.EqualTo(2));
            Assert.That(since3[0].Sequence, Is.EqualTo(4));
            Assert.That(since3[1].Sequence, Is.EqualTo(5));
            Assert.That(since3[0], Is.InstanceOf<EntityRemovedDelta>());
            Assert.That(((EntityRemovedDelta)since3[0]).EntityId, Is.EqualTo("e4"));

            var all = await store.GetMapDeltasSinceSequenceAsync("w", "r", 0);
            Assert.That(all, Has.Length.EqualTo(5));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, all.Select(d => d.Sequence).ToArray());
        }

        [Test]
        public async Task Map_Delta_Compaction_Drops_Entries_At_Or_Below_Sequence()
        {
            var store = NewStore();
            for (long i = 1; i <= 10; i++)
            {
                await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta
                {
                    Sequence = i,
                    EntityId = $"e{i}",
                });
            }

            await store.CompactMapDeltasAsync("w", "r", throughSequence: 6);

            var remaining = await store.GetMapDeltasSinceSequenceAsync("w", "r", 0);
            Assert.That(remaining, Has.Length.EqualTo(4));
            CollectionAssert.AreEqual(new long[] { 7, 8, 9, 10 }, remaining.Select(d => d.Sequence).ToArray());
        }

        [Test]
        public async Task Map_Delta_Polymorphic_Roundtrip_Preserves_Subtype()
        {
            var store = NewStore();
            await store.AppendMapDeltaAsync("w", "r", new EntityMovedDelta
            {
                Sequence = 1, MapId = "m", EntityId = "alice",
                OldX = 0, OldY = 0, OldZ = 0,
                NewX = 3, NewY = 4, NewZ = 0,
            });
            await store.AppendMapDeltaAsync("w", "r", new DoorStateChangedDelta
            {
                Sequence = 2, MapId = "m", EntityId = "door-1",
                IsOpen = true, IsLocked = false,
            });

            var deltas = await store.GetMapDeltasSinceSequenceAsync("w", "r", 0);
            Assert.That(deltas, Has.Length.EqualTo(2));
            Assert.That(deltas[0], Is.InstanceOf<EntityMovedDelta>());
            Assert.That(deltas[1], Is.InstanceOf<DoorStateChangedDelta>());
            var move = (EntityMovedDelta)deltas[0];
            Assert.That(move.NewX, Is.EqualTo(3));
            Assert.That(move.NewY, Is.EqualTo(4));
            var door = (DoorStateChangedDelta)deltas[1];
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public async Task Worlds_And_Regions_Are_Isolated()
        {
            var store = NewStore();
            await store.AppendMapDeltaAsync("world-A", "r1", new EntityRemovedDelta { Sequence = 1, EntityId = "a" });
            await store.AppendMapDeltaAsync("world-B", "r1", new EntityRemovedDelta { Sequence = 1, EntityId = "b" });
            await store.AppendMapDeltaAsync("world-A", "r2", new EntityRemovedDelta { Sequence = 1, EntityId = "c" });

            var aR1 = await store.GetMapDeltasSinceSequenceAsync("world-A", "r1", 0);
            var bR1 = await store.GetMapDeltasSinceSequenceAsync("world-B", "r1", 0);
            var aR2 = await store.GetMapDeltasSinceSequenceAsync("world-A", "r2", 0);

            Assert.That(((EntityRemovedDelta)aR1[0]).EntityId, Is.EqualTo("a"));
            Assert.That(((EntityRemovedDelta)bR1[0]).EntityId, Is.EqualTo("b"));
            Assert.That(((EntityRemovedDelta)aR2[0]).EntityId, Is.EqualTo("c"));
        }

        [Test]
        public async Task Delete_Snapshot_Also_Clears_Both_Delta_Logs()
        {
            var store = NewStore();
            var snapshot = new RegionStateSnapshot { RegionId = "r", MapId = "m" };
            await store.SaveSnapshotAsync("w", snapshot);
            await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta { Sequence = 1, EntityId = "e" });
            await store.AppendDeltaAsync("w", "r", new RegionDelta { RegionId = "r", Timestamp = DateTime.UtcNow });

            await store.DeleteSnapshotAsync("w", "r");

            Assert.That(await store.LoadSnapshotAsync("w", "r"), Is.Null);
            Assert.That(await store.GetMapDeltasSinceSequenceAsync("w", "r", 0), Is.Empty);
            Assert.That(await store.GetDeltasSinceAsync("w", "r", DateTime.MinValue), Is.Empty);
        }

        [Test]
        public async Task List_Region_Ids_Returns_Only_Saved_Regions()
        {
            var store = NewStore();
            await store.SaveSnapshotAsync("w", new RegionStateSnapshot { RegionId = "alpha", MapId = "m" });
            await store.SaveSnapshotAsync("w", new RegionStateSnapshot { RegionId = "beta", MapId = "m" });
            await store.SaveSnapshotAsync("other", new RegionStateSnapshot { RegionId = "gamma", MapId = "m" });

            var ids = await store.ListRegionIdsAsync("w");
            CollectionAssert.AreEquivalent(new[] { "alpha", "beta" }, ids);
        }

        [Test]
        public async Task Compact_Region_Deltas_Saves_Snapshot_And_Clears_Legacy_Log()
        {
            var store = NewStore();
            await store.AppendDeltaAsync("w", "r", new RegionDelta { RegionId = "r", Timestamp = DateTime.UtcNow });
            await store.AppendDeltaAsync("w", "r", new RegionDelta { RegionId = "r", Timestamp = DateTime.UtcNow });

            await store.CompactDeltasAsync("w", "r",
                new RegionStateSnapshot { RegionId = "r", MapId = "m", GameTimeHours = 13.5 });

            var snap = await store.LoadSnapshotAsync("w", "r");
            Assert.That(snap, Is.Not.Null);
            Assert.That(snap!.GameTimeHours, Is.EqualTo(13.5).Within(0.0001));
            var remaining = await store.GetDeltasSinceAsync("w", "r", DateTime.MinValue);
            Assert.That(remaining, Is.Empty);
        }

        [Test]
        public async Task Duplicate_Sequence_Append_Throws()
        {
            var store = NewStore();
            await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta { Sequence = 5, EntityId = "e" });
            Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(async () =>
                await store.AppendMapDeltaAsync("w", "r", new EntityRemovedDelta { Sequence = 5, EntityId = "duplicate" }));
        }
    }
}
