using System;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Temporal modifier that spawns creatures based on time, weather, and season.
    /// Uses SpawnManager to select appropriate creatures based on conditions.
    /// </summary>
    public class SpawnModifier : ITemporalModifier
    {
        private readonly SpawnManager _spawnManager;
        private readonly Random _random;
        private readonly double _spawnProbabilityPerTick; // Probability of attempting to spawn per tick

        public string Name => "spawn";
        public int Priority => 50; // Medium priority - runs after weather/season updates

        public SpawnModifier(SpawnManager spawnManager, double spawnProbabilityPerTick = 0.01)
        {
            _spawnManager = spawnManager ?? throw new ArgumentNullException(nameof(spawnManager));
            _random = new Random();
            _spawnProbabilityPerTick = spawnProbabilityPerTick;
        }

        public async Task ApplyAsync(
            IMapRegionGrain region,
            RegionStateSnapshot regionSnapshot,
            TimeSpan gameTimeElapsed,
            double timeOfDay,
            int day)
        {
            // Check if we should attempt spawning this tick
            // Lower probability for smaller time elapsed (more frequent ticks = fewer spawn attempts)
            var adjustedProbability = _spawnProbabilityPerTick * (gameTimeElapsed.TotalSeconds / 60.0); // Scale by time elapsed
            if (_random.NextDouble() > adjustedProbability)
                return;

            var regionId = regionSnapshot.RegionId;

            // Select a creature to spawn based on current conditions
            var creatureType = _spawnManager.SelectSpawn(regionId, timeOfDay, day);
            if (creatureType == null)
                return;

            // Calculate spawn rate to determine if we should actually spawn
            var spawnRate = _spawnManager.GetSpawnRate(creatureType, regionId, timeOfDay, day);
            if (spawnRate <= 0)
                return;

            // Spawn the creature
            // Note: Actual entity creation would need World access, which isn't directly available from IMapRegionGrain
            // For now, we record spawn intent in the region snapshot
            // TODO: Integrate with GameMapGrain to get World reference and spawn actual entities
            await RecordSpawnIntentAsync(region, regionSnapshot, creatureType, spawnRate);
        }

        private async Task RecordSpawnIntentAsync(
            IMapRegionGrain region,
            RegionStateSnapshot snapshot,
            string creatureType,
            double spawnRate)
        {
            // Record spawn intent in region state
            // In a full implementation, this would:
            // 1. Get World reference from GameMapGrain
            // 2. Find a suitable spawn location in the region
            // 3. Create the entity and add it to the world
            // 4. Update region snapshot with new entity

            // For now, we can log it or store spawn intents in the snapshot
            // This allows the system to track spawn patterns even if actual entity creation is deferred
            
            var delta = new RegionDelta
            {
                RegionId = snapshot.RegionId,
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.EntityAdded,
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["creatureType"] = creatureType,
                    ["spawnRate"] = spawnRate,
                    ["action"] = "spawn_intent"
                }
            };

            await region.ApplyDeltaAsync(delta);
        }
    }
}

