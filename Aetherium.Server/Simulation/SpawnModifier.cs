using System;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Components;
using Microsoft.Extensions.DependencyInjection;
using WorldLocation = Aetherium.Components.WorldLocation;

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
        private readonly IServiceProvider? _serviceProvider;

        public string Name => "spawn";
        public int Priority => 50; // Medium priority - runs after weather/season updates

        public SpawnModifier(SpawnManager spawnManager, double spawnProbabilityPerTick = 0.01, IServiceProvider? serviceProvider = null)
        {
            _spawnManager = spawnManager ?? throw new ArgumentNullException(nameof(spawnManager));
            _random = new Random();
            _spawnProbabilityPerTick = spawnProbabilityPerTick;
            _serviceProvider = serviceProvider;
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
            await SpawnCreatureAsync(region, regionSnapshot, creatureType, spawnRate);
        }

        private async Task SpawnCreatureAsync(
            IMapRegionGrain region,
            RegionStateSnapshot snapshot,
            string creatureType,
            double spawnRate)
        {
            // Get MapId from snapshot
            if (string.IsNullOrEmpty(snapshot.MapId))
                return;

            // Resolve GameMapGrain to get World access
            if (_serviceProvider == null)
                return;

            var grainFactory = _serviceProvider.GetService<Orleans.IGrainFactory>();
            if (grainFactory == null)
                return;

            var mapGrain = grainFactory.GetGrain<IGameMapGrain>(snapshot.MapId);

            // Find a suitable spawn location within the region
            // Calculate region bounds
            var regionX = snapshot.RegionX;
            var regionY = snapshot.RegionY;
            var regionSize = snapshot.RegionSize;
            var z = snapshot.ZLevel;

            // Search for a passable location within the region bounds
            var attempts = 0;
            var maxAttempts = 50;
            WorldLocation? spawnLocation = null;

            while (attempts < maxAttempts && spawnLocation == null)
            {
                attempts++;

                // Random location within region bounds
                var x = regionX * regionSize + _random.Next(0, regionSize);
                var y = regionY * regionSize + _random.Next(0, regionSize);
                var location = new WorldLocation(x, y, z);

                // Request spawn at this location
                var request = new SpawnEntityRequest
                {
                    CreatureType = creatureType,
                    X = x,
                    Y = y,
                    Z = z,
                    SpawnRate = spawnRate
                };

                var result = await mapGrain.SpawnEntityAsync(request);
                if (result.Success && !string.IsNullOrEmpty(result.EntityId))
                {
                    spawnLocation = location;

                    // Record spawn in region delta
                    var delta = new RegionDelta
                    {
                        RegionId = snapshot.RegionId,
                        Timestamp = DateTime.UtcNow,
                        Type = DeltaType.EntityAdded,
                        Data = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["creatureType"] = creatureType,
                            ["location"] = location.ToString(),
                            ["entityId"] = result.EntityId,
                            ["spawnRate"] = spawnRate
                        }
                    };

                    await region.ApplyDeltaAsync(delta);
                    break;
                }
            }
        }

    }
}

