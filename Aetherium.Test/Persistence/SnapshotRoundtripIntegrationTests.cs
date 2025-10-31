using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.Persistence
{
    [TestFixture]
    public class SnapshotRoundtripIntegrationTests
    {
        [Test]
        public async Task SaveLoadSnapshot_WithDeltas_AppliesDeltasOnLoad()
        {
            var store = new MemoryWorldSnapshotStore();
            var worldId = "test-world";
            var regionId = "region:0,0,0";

            // Create initial snapshot
            var baseSnapshot = new RegionStateSnapshot
            {
                RegionId = regionId,
                MapId = "map:0",
                RegionX = 0,
                RegionY = 0,
                ZLevel = 0,
                RegionSize = 64,
                GameTimeHours = 0.0
            };

            await store.SaveSnapshotAsync(worldId, baseSnapshot);

            // Append several deltas
            await store.AppendDeltaAsync(worldId, regionId, new RegionDelta
            {
                RegionId = regionId,
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.TerrainModified,
                Data = new Dictionary<string, object>
                {
                    ["location"] = "10,20",
                    ["terrainType"] = "stone"
                }
            });

            await store.AppendDeltaAsync(worldId, regionId, new RegionDelta
            {
                RegionId = regionId,
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.TraversalRecorded,
                Data = new Dictionary<string, object>
                {
                    ["location"] = "5,5",
                    ["count"] = 3
                }
            });

            // Compact deltas
            await store.CompactDeltasAsync(worldId, regionId, baseSnapshot);

            // Load and verify
            var loaded = await store.LoadSnapshotAsync(worldId, regionId);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.TerrainModifications.ContainsKey("10,20"), Is.True);
            Assert.That(loaded.TerrainModifications["10,20"], Is.EqualTo("stone"));
            Assert.That(loaded.TraversalHeatmap.ContainsKey("5,5"), Is.True);
            Assert.That(loaded.TraversalHeatmap["5,5"], Is.EqualTo(3));
        }

        [Test]
        public async Task GetDeltasSince_ReturnsOnlyRecentDeltas()
        {
            var store = new MemoryWorldSnapshotStore();
            var worldId = "test-world";
            var regionId = "region:0,0,0";

            var baseTime = DateTime.UtcNow.AddHours(-2);
            var recentTime = DateTime.UtcNow.AddHours(-1);

            // Add old delta
            await store.AppendDeltaAsync(worldId, regionId, new RegionDelta
            {
                RegionId = regionId,
                Timestamp = baseTime,
                Type = DeltaType.TerrainModified,
                Data = new Dictionary<string, object> { ["location"] = "1,1", ["terrainType"] = "grass" }
            });

            // Add recent delta
            await store.AppendDeltaAsync(worldId, regionId, new RegionDelta
            {
                RegionId = regionId,
                Timestamp = recentTime,
                Type = DeltaType.TerrainModified,
                Data = new Dictionary<string, object> { ["location"] = "2,2", ["terrainType"] = "stone" }
            });

            // Get deltas since baseTime
            var deltas = await store.GetDeltasSinceAsync(worldId, regionId, baseTime);

            Assert.That(deltas.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(deltas.Any(d => d.Data.ContainsKey("location") && d.Data["location"]?.ToString() == "2,2"), Is.True);
        }

        [Test]
        public async Task CompactDeltas_ClearsDeltaLog()
        {
            var store = new MemoryWorldSnapshotStore();
            var worldId = "test-world";
            var regionId = "region:0,0,0";

            var baseSnapshot = new RegionStateSnapshot
            {
                RegionId = regionId,
                MapId = "map:0",
                RegionX = 0,
                RegionY = 0,
                ZLevel = 0,
                RegionSize = 64,
                GameTimeHours = 0.0
            };

            await store.SaveSnapshotAsync(worldId, baseSnapshot);

            // Add a delta
            await store.AppendDeltaAsync(worldId, regionId, new RegionDelta
            {
                RegionId = regionId,
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.TerrainModified,
                Data = new Dictionary<string, object>
                {
                    ["location"] = "10,20",
                    ["terrainType"] = "stone"
                }
            });

            // Compact
            await store.CompactDeltasAsync(worldId, regionId, baseSnapshot);

            // Verify delta log is cleared
            var deltas = await store.GetDeltasSinceAsync(worldId, regionId, DateTime.MinValue);
            Assert.That(deltas.Length, Is.EqualTo(0));
        }
    }
}

