using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.Persistence
{
	[TestFixture]
	public class WorldSnapshotStoreTests
	{
		[Test]
		public async Task SaveLoadSnapshot_Roundtrip()
		{
			var store = new MemoryWorldSnapshotStore();
			var worldId = "test-world";
			var regionId = "region:0,0,0";

			var snapshot = new RegionStateSnapshot
			{
				RegionId = regionId,
				MapId = "map:0",
				RegionX = 0,
				RegionY = 0,
				ZLevel = 0,
				RegionSize = 64,
				GameTimeHours = 12.5,
				TerrainModifications = new Dictionary<string, string> { ["10,20"] = "stone" },
				TraversalHeatmap = new Dictionary<string, int> { ["5,5"] = 3 }
			};

			await store.SaveSnapshotAsync(worldId, snapshot);
			var loaded = await store.LoadSnapshotAsync(worldId, regionId);

			Assert.That(loaded, Is.Not.Null);
			Assert.That(loaded!.RegionId, Is.EqualTo(regionId));
			Assert.That(loaded.GameTimeHours, Is.EqualTo(12.5).Within(0.0001));
			Assert.That(loaded.TerrainModifications["10,20"], Is.EqualTo("stone"));
			Assert.That(loaded.TraversalHeatmap["5,5"], Is.EqualTo(3));
		}

		[Test]
		public async Task AppendAndGetDeltasSince_Works()
		{
			var store = new MemoryWorldSnapshotStore();
			var worldId = "test-world";
			var regionId = "region:0,0,0";

			var since = DateTime.UtcNow.AddMinutes(-1);
			var delta = new RegionDelta
			{
				RegionId = regionId,
				Timestamp = DateTime.UtcNow,
				Type = DeltaType.TerrainModified,
				Data = new Dictionary<string, object>
				{
					["location"] = "1,2",
					["terrainType"] = "grass"
				}
			};

			await store.AppendDeltaAsync(worldId, regionId, delta);
			var deltas = await store.GetDeltasSinceAsync(worldId, regionId, since);

			Assert.That(deltas, Is.Not.Null);
			Assert.That(deltas.Length, Is.GreaterThanOrEqualTo(1));
		}

		[Test]
		public async Task CompactDeltas_AppliesDeltasAndClearsLog()
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

			// Append a couple of deltas
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
					["count"] = 7
				}
			});

			await store.CompactDeltasAsync(worldId, regionId, baseSnapshot);

			var loaded = await store.LoadSnapshotAsync(worldId, regionId);
			Assert.That(loaded, Is.Not.Null);
			Assert.That(loaded!.TerrainModifications.ContainsKey("10,20"), Is.True);
			Assert.That(loaded.TerrainModifications["10,20"], Is.EqualTo("stone"));
			Assert.That(loaded.TraversalHeatmap.ContainsKey("5,5"), Is.True);
			Assert.That(loaded.TraversalHeatmap["5,5"], Is.EqualTo(7));

			// Delta log should be cleared
			var deltasAfter = await store.GetDeltasSinceAsync(worldId, regionId, DateTime.MinValue);
			Assert.That(deltasAfter.Length, Is.EqualTo(0));
		}
	}
}


