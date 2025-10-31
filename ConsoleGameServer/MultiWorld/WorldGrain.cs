using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleGameServer.MultiWorld
{
    /// <summary>
    /// Orleans grain coordinating a multi-map world.
    /// </summary>
    public class WorldGrain : Grain, IWorldGrain
    {
        private readonly IPersistentState<WorldGrainState> _worldState;
        private readonly IGrainFactory _grainFactory;

        public WorldGrain(
            [PersistentState("world", "worldStore")] IPersistentState<WorldGrainState> worldState,
            IGrainFactory grainFactory)
        {
            _worldState = worldState;
            _grainFactory = grainFactory;
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
                MapIds = new List<string>()
            };

            _worldState.State.Info.LastActivityAt = DateTime.UtcNow;

            // Create initial map
            var initialMapId = await AddMapAsync(
                "Main",
                config.GeneratorType,
                config.GeneratorParameters);

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

            await mapGrain.InitializeAsync(_worldState.State.Info.WorldId, mapName, size, generatorType, parameters);

            _worldState.State.Info.MapIds.Add(mapId);
            await _worldState.WriteStateAsync();

            return mapId;
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

            // Tick all maps
            var tickTasks = _worldState.State.Info.MapIds
                .Select(mapId => _grainFactory.GetGrain<IGameMapGrain>(mapId).TickAsync())
                .ToList();

            await Task.WhenAll(tickTasks);

            _worldState.State.Info.LastActivityAt = DateTime.UtcNow;
            // Note: Not persisting on every tick to avoid excessive writes
        }
    }

    /// <summary>
    /// Persisted state for a world grain.
    /// </summary>
    public class WorldGrainState
    {
        public WorldInfo Info { get; set; } = new WorldInfo();
        public Dictionary<string, string> PlayerLocations { get; set; } = new Dictionary<string, string>(); // PlayerId -> MapId
    }
}

