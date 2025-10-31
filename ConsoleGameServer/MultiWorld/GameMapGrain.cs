using Orleans;
using Orleans.Runtime;
using ConsoleGame.Core;
using ConsoleGame.WorldGen;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleGameServer.MultiWorld
{
    /// <summary>
    /// Orleans grain managing a single game map within a world.
    /// </summary>
    public class GameMapGrain : Grain, IGameMapGrain
    {
        private readonly IPersistentState<MapState> _mapState;
        private readonly MapGeneratorRegistry _generatorRegistry;
        private World? _world;

        public GameMapGrain(
            [PersistentState("map", "mapStore")] IPersistentState<MapState> mapState,
            MapGeneratorRegistry generatorRegistry)
        {
            _mapState = mapState;
            _generatorRegistry = generatorRegistry;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_mapState.State != null && _mapState.State.SerializedWorld != null)
            {
                // Restore world from persisted state
                // In full implementation, deserialize the world
                _world = new World(); // Placeholder
            }

            await base.OnActivateAsync(cancellationToken);
        }

        public async Task InitializeAsync(string worldId, string mapName, WorldSize size, string generatorType, Dictionary<string, object> parameters)
        {
            var mapId = this.GetPrimaryKeyString();
            
            // Create generation context
            var context = new GeneratorContext(size.Width, size.Height)
            {
                ZLevel = 0,
                Seed = (int)DateTime.UtcNow.Ticks // Use deterministic seed in production
            };

            // TODO: Copy parameters to context when GeneratorContext supports parameters dictionary

            // Generate the world
            var generator = _generatorRegistry.GetGenerator(generatorType);
            if (generator == null)
            {
                throw new InvalidOperationException($"Generator '{generatorType}' not found");
            }

            _world = generator.Generate(context);

            // Update state
            _mapState.State = new MapState
            {
                MapId = mapId,
                WorldId = worldId,
                MapName = mapName,
                Size = size,
                GeneratorType = generatorType,
                PlayerIds = new HashSet<string>(),
                CreatedAt = DateTime.UtcNow,
                SerializedWorld = null // TODO: Serialize world for persistence
            };

            await _mapState.WriteStateAsync();
        }

        public Task<string?> GetWorldAsync()
        {
            // TODO: Return serialized world when implemented
            return Task.FromResult<string?>(null);
        }

        public Task<MapMetadata?> GetMetadataAsync()
        {
            if (_mapState.State == null)
                return Task.FromResult<MapMetadata?>(null);

            var metadata = new MapMetadata
            {
                MapId = _mapState.State.MapId,
                WorldId = _mapState.State.WorldId,
                MapName = _mapState.State.MapName,
                Size = _mapState.State.Size,
                GeneratorType = _mapState.State.GeneratorType,
                PlayerCount = _mapState.State.PlayerIds.Count,
                CreatedAt = _mapState.State.CreatedAt
            };

            return Task.FromResult<MapMetadata?>(metadata);
        }

        public async Task<bool> AddPlayerAsync(string playerId)
        {
            if (_mapState.State == null)
                return false;

            bool added = _mapState.State.PlayerIds.Add(playerId);
            if (added)
            {
                await _mapState.WriteStateAsync();
            }

            return added;
        }

        public async Task RemovePlayerAsync(string playerId)
        {
            if (_mapState.State == null)
                return;

            bool removed = _mapState.State.PlayerIds.Remove(playerId);
            if (removed)
            {
                await _mapState.WriteStateAsync();
            }
        }

        public Task<List<string>> GetPlayersAsync()
        {
            if (_mapState.State == null)
                return Task.FromResult(new List<string>());

            return Task.FromResult(new List<string>(_mapState.State.PlayerIds));
        }

        public Task TickAsync()
        {
            // TODO: Process game logic for this map
            // - NPC movement
            // - Environmental effects
            // - Quest triggers
            // - etc.

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Persisted state for a game map.
    /// </summary>
    [GenerateSerializer]
    public class MapState
    {
        [Id(0)] public string MapId { get; set; } = string.Empty;
        [Id(1)] public string WorldId { get; set; } = string.Empty;
        [Id(2)] public string MapName { get; set; } = string.Empty;
        [Id(3)] public WorldSize Size { get; set; } = new WorldSize();
        [Id(4)] public string GeneratorType { get; set; } = string.Empty;
        [Id(5)] public HashSet<string> PlayerIds { get; set; } = new HashSet<string>();
        [Id(6)] public DateTime CreatedAt { get; set; }
        [Id(7)] public byte[]? SerializedWorld { get; set; } // Serialized World object
    }
}

