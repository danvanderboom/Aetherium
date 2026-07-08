using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain coordinating a multi-map world.
    /// </summary>
    [Orleans.Concurrency.Reentrant]
    public class WorldGrain : Grain, IWorldGrain
    {
        private readonly IPersistentState<WorldGrainState> _worldState;
        private readonly IGrainFactory _grainFactory;
        private WorldClock? _clock;

        public WorldGrain(
            [PersistentState("world", "worldStore")] IPersistentState<WorldGrainState> worldState,
            IGrainFactory grainFactory)
        {
            _worldState = worldState;
            _grainFactory = grainFactory;
        }

        private WorldClock GetClock()
        {
            if (_clock == null)
            {
                _clock = this.ServiceProvider.GetService<WorldClock>();
                // If clock not available, create a default one (for tests)
                _clock ??= new WorldClock(Microsoft.Extensions.Options.Options.Create(new SimulationOptions
                {
                    TickHz = 1,
                    DayLengthMinutes = 24,
                    RegionSize = 64,
                    EnableWeather = true,
                    EnableSeasons = true
                }));
            }
            return _clock;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // Initialize state if needed
            if (_worldState.State == null)
            {
                _worldState.State = new WorldGrainState
                {
                    Info = new WorldInfo
                    {
                        WorldId = this.GetPrimaryKeyString(),
                        State = WorldState.Creating
                    },
                    PlayerLocations = new Dictionary<string, string>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task InitializeAsync(WorldConfig config)
        {
            config.WorldId = this.GetPrimaryKeyString();

            _worldState.State.Info = new WorldInfo
            {
                WorldId = config.WorldId,
                Name = config.Name,
                Description = config.Description,
                State = WorldState.Creating,
                PlayerCount = 0,
                MaxPlayers = config.MaxPlayers,
                CreatedAt = config.CreatedAt,
                NarrativeId = config.NarrativeId,
                MapIds = new List<string>(),
                ClusterId = config.ClusterId
            };
            _worldState.State.DeathPolicy = config.DeathPolicy;

            _worldState.State.Info.LastActivityAt = DateTime.UtcNow;

            // Register with cluster if ClusterId is set
            if (!string.IsNullOrEmpty(config.ClusterId))
            {
                var clusterGrain = _grainFactory.GetGrain<IClusterGrain>(config.ClusterId);
                await clusterGrain.RegisterWorldAsync(config.WorldId);
            }

            // Create initial map (propagate requested size into generator parameters)
            var parameters = config.GeneratorParameters ?? new Dictionary<string, object>();
            if (config.Size != null)
            {
                parameters["Width"] = config.Size.Width;
                parameters["Height"] = config.Size.Height;
                parameters["Depth"] = config.Size.Depth;
            }

            var initialMapId = await AddMapAsync(
                "Main",
                config.GeneratorType,
                parameters);

            _worldState.State.Info.State = WorldState.Active;
            await _worldState.WriteStateAsync();
        }

        public Task<WorldInfo?> GetInfoAsync()
        {
            return Task.FromResult<WorldInfo?>(_worldState.State.Info);
        }

        public Task<WorldState> GetStateAsync()
        {
            return Task.FromResult(_worldState.State.Info.State);
        }

        public async Task PauseAsync()
        {
            _worldState.State.Info.State = WorldState.Paused;
            await _worldState.WriteStateAsync();
        }

        public async Task ResumeAsync()
        {
            if (_worldState.State.Info.State == WorldState.Paused)
            {
                _worldState.State.Info.State = WorldState.Active;
                await _worldState.WriteStateAsync();
            }
        }

        public async Task ShutdownAsync()
        {
            _worldState.State.Info.State = WorldState.ShuttingDown;

            // Remove all players
            foreach (var playerId in _worldState.State.PlayerLocations.Keys.ToList())
            {
                await RemovePlayerAsync(playerId);
            }

            _worldState.State.Info.State = WorldState.Stopped;
            await _worldState.WriteStateAsync();

            // TODO: Optionally deactivate all map grains
        }

        public async Task<string> AddMapAsync(string mapName, string generatorType, Dictionary<string, object> parameters)
        {
            var mapId = $"{_worldState.State.Info.WorldId}:map:{Guid.NewGuid()}";
            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(mapId);

            // Get world size from config or use default
            var size = new WorldSize { Width = 100, Height = 100, Depth = 1 };
            if (parameters.ContainsKey("Width"))
                size.Width = Convert.ToInt32(parameters["Width"]);
            if (parameters.ContainsKey("Height"))
                size.Height = Convert.ToInt32(parameters["Height"]);

            await mapGrain.InitializeAsync(_worldState.State.Info.WorldId, mapName, size, generatorType, parameters, _worldState.State.DeathPolicy);

            _worldState.State.Info.MapIds.Add(mapId);
            await _worldState.WriteStateAsync();

            return mapId;
        }

        public async Task<bool> RemoveMapAsync(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || !_worldState.State.Info.MapIds.Remove(mapId))
                return false;

            // Drop any player locations pointing at the removed map so the world's player
            // bookkeeping doesn't reference a map that is no longer ticked. (Instance maps are
            // normally emptied before release, but this keeps state consistent either way.)
            var strandedPlayers = _worldState.State.PlayerLocations
                .Where(kvp => kvp.Value == mapId)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var playerId in strandedPlayers)
            {
                _worldState.State.PlayerLocations.Remove(playerId);
                if (_worldState.State.Info.PlayerCount > 0)
                    _worldState.State.Info.PlayerCount--;
            }

            await _worldState.WriteStateAsync();
            return true;
        }

        public Task<List<string>> GetMapIdsAsync()
        {
            return Task.FromResult(_worldState.State.Info.MapIds);
        }

        public async Task<bool> AddPlayerAsync(string playerId, string? mapId = null)
        {
            if (_worldState.State.Info.State != WorldState.Active)
                return false;

            if (_worldState.State.Info.PlayerCount >= _worldState.State.Info.MaxPlayers)
                return false;

            // Choose default map if not specified
            if (mapId == null && _worldState.State.Info.MapIds.Count > 0)
            {
                mapId = _worldState.State.Info.MapIds[0];
            }

            if (mapId == null)
                return false;

            // Add player to map
            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(mapId);
            bool added = await mapGrain.AddPlayerAsync(playerId);

            if (added)
            {
                _worldState.State.PlayerLocations[playerId] = mapId;
                _worldState.State.Info.PlayerCount++;
                _worldState.State.Info.LastActivityAt = DateTime.UtcNow;
                await _worldState.WriteStateAsync();
            }

            return added;
        }

        public async Task RemovePlayerAsync(string playerId)
        {
            if (!_worldState.State.PlayerLocations.TryGetValue(playerId, out var mapId))
                return;

            // Remove from map
            var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(mapId);
            await mapGrain.RemovePlayerAsync(playerId);

            // Remove from world tracking
            _worldState.State.PlayerLocations.Remove(playerId);
            _worldState.State.Info.PlayerCount--;
            await _worldState.WriteStateAsync();
        }

        public async Task<bool> MovePlayerToMapAsync(string playerId, string targetMapId)
        {
            if (!_worldState.State.PlayerLocations.TryGetValue(playerId, out var currentMapId))
                return false;

            if (currentMapId == targetMapId)
                return true; // Already there

            if (!_worldState.State.Info.MapIds.Contains(targetMapId))
                return false;

            // Remove from current map
            var currentMapGrain = _grainFactory.GetGrain<IGameMapGrain>(currentMapId);
            await currentMapGrain.RemovePlayerAsync(playerId);

            // Add to target map
            var targetMapGrain = _grainFactory.GetGrain<IGameMapGrain>(targetMapId);
            bool added = await targetMapGrain.AddPlayerAsync(playerId);

            if (added)
            {
                _worldState.State.PlayerLocations[playerId] = targetMapId;
                await _worldState.WriteStateAsync();
            }

            return added;
        }

        public Task<string?> GetPlayerMapAsync(string playerId)
        {
            _worldState.State.PlayerLocations.TryGetValue(playerId, out var mapId);
            return Task.FromResult<string?>(mapId);
        }

        public async Task TickAsync()
        {
            if (_worldState.State.Info.State != WorldState.Active)
                return;

            // Get game time elapsed from clock
            var clock = GetClock();
            var gameTimeElapsed = clock.Tick(); // Advances clock and returns elapsed time
            
            // Tick all maps with game time
            var tickTasks = _worldState.State.Info.MapIds
                .Select(mapId => _grainFactory.GetGrain<IGameMapGrain>(mapId).TickAsync(gameTimeElapsed))
                .ToList();

            await Task.WhenAll(tickTasks);

            _worldState.State.Info.LastActivityAt = DateTime.UtcNow;
            // Note: Not persisting on every tick to avoid excessive writes
        }

        public async Task SaveWorldAsync()
        {
            if (_worldState.State == null || _worldState.State.Info.State == WorldState.Creating)
                return;

            // Save all maps (which will save their regions)
            var saveTasks = _worldState.State.Info.MapIds
                .Select(mapId => _grainFactory.GetGrain<IGameMapGrain>(mapId).SaveMapAsync())
                .ToList();

            await Task.WhenAll(saveTasks);

            // Save world metadata
            _worldState.State.Info.LastActivityAt = DateTime.UtcNow;
            await _worldState.WriteStateAsync();
        }

        public async Task<bool> LoadWorldAsync()
        {
            if (_worldState.State == null)
                return false;

            // Load all maps
            var loadTasks = _worldState.State.Info.MapIds
                .Select(mapId => _grainFactory.GetGrain<IGameMapGrain>(mapId).LoadMapAsync())
                .ToList();

            var results = await Task.WhenAll(loadTasks);
            return results.All(r => r);
        }
    }

    /// <summary>
    /// Persisted state for a world grain.
    /// </summary>
    public class WorldGrainState
    {
        public WorldInfo Info { get; set; } = new WorldInfo();
        public Dictionary<string, string> PlayerLocations { get; set; } = new Dictionary<string, string>(); // PlayerId -> MapId

        /// <summary>Per-world death/respawn rules (engine gap-analysis §4.11), set once at
        /// InitializeAsync and applied to every map this world creates (initial and later). Null
        /// means every map falls back to <see cref="Aetherium.Model.Combat.DeathPolicy.Default"/>.</summary>
        public Aetherium.Model.Combat.DeathPolicy? DeathPolicy { get; set; }
    }
}


