using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Events;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain managing entity spawns during events.
    /// Coordinates entity creation and cleanup for event instances.
    /// </summary>
    public class SpawnControllerGrain : Grain, ISpawnControllerGrain
    {
        private readonly IPersistentState<SpawnControllerState> _state;
        private readonly IGrainFactory _grainFactory;

        public SpawnControllerGrain(
            [PersistentState("spawnController", "worldStore")] IPersistentState<SpawnControllerState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new SpawnControllerState
                {
                    EventInstanceId = this.GetPrimaryKeyString(),
                    SpawnedEntities = new List<string>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<SpawnResult> SpawnEntitiesAsync(
            string eventType,
            Dictionary<string, object> spawnConfig,
            string mapId,
            int x, int y, int z,
            int count)
        {
            var entityIds = new List<string>();
            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(mapId);

            // Get spawn type from config
            var spawnType = spawnConfig.TryGetValue("spawnType", out var typeObj)
                ? typeObj?.ToString() ?? "Monster"
                : "Monster";

            // Resolve optional spawnRate from config; default to 1.0 (always spawn)
            double spawnRate = 1.0;
            if (spawnConfig.TryGetValue("spawnRate", out var rateObj))
            {
                if (rateObj is double d) spawnRate = d;
                else if (rateObj is float f) spawnRate = f;
                else if (rateObj is int i) spawnRate = i;
                else if (double.TryParse(rateObj?.ToString(), out var parsed)) spawnRate = parsed;
            }

            // Spawn entities
            for (int i = 0; i < count; i++)
            {
                try
                {
                    // Respect spawn rate deterministically: <=0 skip; >=1 spawn
                    if (spawnRate <= 0)
                    {
                        continue;
                    }

                    // Calculate spawn location (spread around event location)
                    var spawnX = x + (i % 3 - 1) * 2; // -2, 0, 2 offset
                    var spawnY = y + (i / 3 - 1) * 2;

                    var request = new SpawnEntityRequest
                    {
                        CreatureType = spawnType,
                        X = spawnX,
                        Y = spawnY,
                        Z = z,
                        SpawnRate = Math.Max(0.0, Math.Min(1.0, spawnRate))
                    };

                    var result = await mapGrain.SpawnEntityAsync(request);
                    if (result.Success && !string.IsNullOrEmpty(result.EntityId))
                    {
                        entityIds.Add(result.EntityId);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue spawning
                    // TODO: Add logging
                }
            }

            // Track spawned entities
            _state.State.SpawnedEntities.AddRange(entityIds);
            await _state.WriteStateAsync();

            return new SpawnResult
            {
                Success = entityIds.Count > 0,
                EntityIds = entityIds,
                ErrorMessage = entityIds.Count < count ? $"Only spawned {entityIds.Count} of {count} entities" : null
            };
        }

        public async Task<bool> DespawnEntitiesAsync(List<string> entityIds)
        {
            // Remove from tracking
            foreach (var entityId in entityIds)
            {
                _state.State.SpawnedEntities.Remove(entityId);
            }

            // TODO: Actually despawn entities from map
            // This would require a RemoveEntityAsync method on IGameMapGrain

            await _state.WriteStateAsync();
            return true;
        }

        public Task<List<string>> GetSpawnedEntitiesAsync()
        {
            return Task.FromResult(new List<string>(_state.State.SpawnedEntities));
        }
    }

    /// <summary>
    /// State for the spawn controller grain.
    /// </summary>
    [GenerateSerializer]
    public class SpawnControllerState
    {
        [Id(0)] public string EventInstanceId { get; set; } = string.Empty;
        [Id(1)] public List<string> SpawnedEntities { get; set; } = new List<string>();
    }
}

