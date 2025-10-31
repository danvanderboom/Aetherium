using Orleans;
using Orleans.Runtime;
using Aetherium.Core;
using Aetherium.WorldGen;
using Passes = Aetherium.WorldGen.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
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
            
            parameters ??= new Dictionary<string, object>();

            var seed = (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
            var context = new GeneratorContext(size.Width, size.Height, seed)
            {
                ZLevel = 0,
                Levels = size.Depth
            };

            var parameterStrings = parameters?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty)
                ?? new Dictionary<string, string>();

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = generatorType,
                Width = size.Width,
                Height = size.Height,
                Levels = size.Depth,
                Seed = seed,
                GeneratorVersion = parameters.TryGetValue("version", out var versionObj) ? versionObj?.ToString() ?? "1.0.0" : "1.0.0",
                Template = ResolveTemplate(generatorType),
                Parameters = parameterStrings
            };

            var passes = BuildPasses(request.Template);
            var orchestrator = new WorldGenerationOrchestrator(_generatorRegistry, passes);
            var result = orchestrator.Generate(request);

            if (!result.Success || result.World == null)
            {
                var details = new List<string>();
                details.AddRange(result.Errors);
                if (result.Validation?.Errors != null)
                    details.AddRange(result.Validation.Errors);
                throw new InvalidOperationException($"Generation failed: {string.Join(", ", details)}");
            }

            _world = result.World;

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

        private static WorldGenerationTemplate ResolveTemplate(string generatorType)
        {
            var normalized = generatorType.ToLowerInvariant();
            if (normalized.Contains("outdoor") || normalized.Contains("terrain"))
                return WorldGenerationTemplate.Outdoor;
            return WorldGenerationTemplate.Dungeon;
        }

        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
        {
            return template switch
            {
                WorldGenerationTemplate.Outdoor => new IWorldGenerationPass[]
                {
                    new Passes.OutdoorLayoutPass(),
                    new Passes.OutdoorThemingPass(),
                    new Passes.OutdoorPopulationPass(),
                    new Passes.EnvironmentalStoryPass(),
                    new Passes.PortalNetworkPass(),
                    new Passes.OutdoorInteractionsPass(),
                    new Passes.AdaptationPass(),
                    new Passes.OutdoorValidationPass()
                },
                _ => new IWorldGenerationPass[]
                {
                    new Passes.DungeonLayoutPass(),
                    new Passes.DungeonThemingPass(),
                    new Passes.DungeonPopulationPass(),
                    new Passes.EnvironmentalStoryPass(),
                    new Passes.PortalNetworkPass(),
                    new Passes.DungeonInteractionsPass(),
                    new Passes.AdaptationPass(),
                    new Passes.DungeonValidationPass()
                }
            };
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


