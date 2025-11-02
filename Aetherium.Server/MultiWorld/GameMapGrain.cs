using Orleans;
using Orleans.Runtime;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
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
        private readonly IGrainFactory _grainFactory;
        private readonly Microsoft.Extensions.Options.IOptions<Aetherium.Server.Simulation.SimulationOptions> _simulationOptions;
        private World? _world;
        private Dictionary<string, IMapRegionGrain>? _regions; // Cache of region grains
        private int _regionSize;

        public GameMapGrain(
            [PersistentState("map", "mapStore")] IPersistentState<MapState> mapState,
            MapGeneratorRegistry generatorRegistry,
            IGrainFactory grainFactory,
            Microsoft.Extensions.Options.IOptions<Aetherium.Server.Simulation.SimulationOptions> simulationOptions)
        {
            _mapState = mapState;
            _generatorRegistry = generatorRegistry;
            _grainFactory = grainFactory;
            _simulationOptions = simulationOptions;
            _regionSize = _simulationOptions.Value.RegionSize;
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

            var seed = parameters.TryGetValue("seed", out var seedObj) && seedObj is int seedInt
                ? seedInt
                : (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
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

            // Partition map into regions and initialize region grains
            await PartitionIntoRegionsAsync();

            // Register map and portals with cluster if world belongs to a cluster
            // Get cluster ID from world grain
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
            var worldInfo = await worldGrain.GetInfoAsync();
            
            if (worldInfo?.ClusterId != null && !string.IsNullOrEmpty(worldInfo.ClusterId))
            {
                var clusterGrain = _grainFactory.GetGrain<IClusterGrain>(worldInfo.ClusterId);
                
                // Register map with cluster (creates market)
                await clusterGrain.RegisterMapAsync(worldId, mapId);
                
                // Find and register portals from the generated world
                await RegisterPortalsWithClusterAsync(clusterGrain, worldId, mapId);
            }

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
                    new Passes.OutdoorInteractionsPass(),
                    new Passes.PortalNetworkPass(), // Place portals for cross-world travel
                    new Passes.OutdoorValidationPass()
                },
                _ => new IWorldGenerationPass[]
                {
                    new Passes.DungeonLayoutPass(),
                    new Passes.DungeonInteractionsPass(),
                    new Passes.PortalNetworkPass(), // Place portals for cross-world travel
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

        public async Task TickAsync(TimeSpan gameTimeElapsed)
        {
            if (_mapState.State == null || _regions == null)
                return;

            // Tick all regions in parallel with game time
            var tickTasks = _regions.Values
                .Select(region => region.TickAsync(gameTimeElapsed))
                .ToList();

            await Task.WhenAll(tickTasks);
        }

        /// <summary>
        /// Partitions the map into regions (64×64 chunks) and initializes region grains.
        /// </summary>
        private async Task PartitionIntoRegionsAsync()
        {
            if (_mapState.State == null || _world == null)
                return;

            var mapId = _mapState.State.MapId;
            var size = _mapState.State.Size;
            
            _regions = new Dictionary<string, IMapRegionGrain>();

            // Calculate number of regions in each dimension
            var regionsX = (int)Math.Ceiling((double)size.Width / _regionSize);
            var regionsY = (int)Math.Ceiling((double)size.Height / _regionSize);

            // Initialize region grains for each Z level
            for (int z = 0; z < size.Depth; z++)
            {
                for (int regionY = 0; regionY < regionsY; regionY++)
                {
                    for (int regionX = 0; regionX < regionsX; regionX++)
                    {
                        var regionKey = GetRegionKey(mapId, regionX, regionY, z);
                        var regionGrain = _grainFactory.GetGrain<IMapRegionGrain>(regionKey);
                        
                        await regionGrain.InitializeAsync(mapId, regionX, regionY, z, _regionSize);
                        
                        _regions[regionKey] = regionGrain;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the region key for a given map, region coordinates, and Z level.
        /// </summary>
        private static string GetRegionKey(string mapId, int regionX, int regionY, int zLevel)
        {
            return $"{mapId}:region:{regionX},{regionY},{zLevel}";
        }

        /// <summary>
        /// Finds portals in the generated world and registers them with the cluster.
        /// </summary>
        private async Task RegisterPortalsWithClusterAsync(IClusterGrain clusterGrain, string worldId, string mapId)
        {
            if (_world == null)
                return;

            // Find all entities with PortalComponent
            foreach (var entity in _world.Entities.Values)
            {
                if (!entity.Has<PortalComponent>())
                    continue;

                var portalComponent = entity.Get<PortalComponent>();
                if (portalComponent == null)
                    continue;

                // Create portal link for registration
                var portalLink = new PortalLink
                {
                    PortalId = portalComponent.PortalId,
                    SourceWorldId = worldId,
                    SourceMapId = mapId,
                    TargetWorldId = portalComponent.TargetWorldId,
                    TargetMapId = portalComponent.TargetMapId,
                    TargetTag = portalComponent.TargetTag,
                    IsResolved = portalComponent.TargetWorldId != null && portalComponent.TargetMapId != null
                };

                await clusterGrain.RegisterPortalAsync(portalLink);
            }
        }

        /// <summary>
        /// Gets the region key for a given world location.
        /// </summary>
        private string GetRegionKeyForLocation(int x, int y, int z)
        {
            if (_mapState.State == null)
                throw new InvalidOperationException("Map not initialized");

            var mapId = _mapState.State.MapId;
            var regionX = x / _regionSize;
            var regionY = y / _regionSize;
            return GetRegionKey(mapId, regionX, regionY, z);
        }

        /// <summary>
        /// Gets the region grain for a given world location.
        /// </summary>
        public IMapRegionGrain? GetRegionForLocation(int x, int y, int z)
        {
            if (_regions == null)
                return null;

            var regionKey = GetRegionKeyForLocation(x, y, z);
            return _regions.TryGetValue(regionKey, out var region) ? region : null;
        }

        public async Task SaveMapAsync()
        {
            if (_regions == null || _mapState.State == null)
                return;

            // Save all regions
            var saveTasks = _regions.Values
                .Select(async region =>
                {
                    var snapshot = await region.GetSnapshotAsync();
                    // Regions persist automatically via Orleans persistent state
                    // This ensures all regions are persisted
                })
                .ToList();

            await Task.WhenAll(saveTasks);

            // Save map metadata
            await _mapState.WriteStateAsync();
        }

        public async Task<bool> LoadMapAsync()
        {
            if (_mapState.State == null)
                return false;

            // Regions are loaded automatically on activation via Orleans persistent state
            // We just need to rebuild the region cache
            await PartitionIntoRegionsAsync();

            return true;
        }

        public async Task<SpawnEntityResult> SpawnEntityAsync(SpawnEntityRequest request)
        {
            if (_world == null)
                return new SpawnEntityResult { Success = false, ErrorMessage = "World not initialized for this map" };

            try
            {
                var location = new WorldLocation(request.X, request.Y, request.Z);

                // Check if location is valid
                if (!_world.PassableTerrain(location))
                {
                    return new SpawnEntityResult { Success = false, ErrorMessage = "Location is not passable" };
                }

                // Check if location is already occupied
                if (_world.EntitiesByLocation.TryGetValue(location, out var entitiesAtLoc))
                {
                    foreach (var existingEntity in entitiesAtLoc.Values)
                    {
                        if (existingEntity is Character)
                        {
                            return new SpawnEntityResult { Success = false, ErrorMessage = "Location is already occupied" };
                        }
                    }
                }

                // Ensure required tile type exists
                if (!_world.TileTypes.ContainsKey("Monster"))
                {
                    _world.TileTypes["Monster"] = new TileType
                    {
                        Name = "Monster",
                        Settings = new Dictionary<string, string>
                        {
                            { "MapCharacter", "M" },
                            { "BackgroundColor", System.ConsoleColor.Black.ToString() },
                            { "ForegroundColor", System.ConsoleColor.DarkRed.ToString() }
                        }
                    };
                }

                // Create the entity based on creature type
                Character? entity = request.CreatureType.ToLowerInvariant() switch
                {
                    "monster" => new Aetherium.Monster(_world),
                    "wolf" => new Aetherium.Monster(_world),
                    "bear" => new Aetherium.Monster(_world),
                    "bandit" => new Aetherium.Monster(_world),
                    "snake" => new Snake(),
                    "zombie" => new Zombie(_world),
                    _ => new Aetherium.Monster(_world)
                };

                if (entity == null)
                {
                    return new SpawnEntityResult { Success = false, ErrorMessage = "Could not create entity" };
                }

                // Set location and add to world
                entity.Set(location);
                _world.AddEntity(entity);

                return new SpawnEntityResult { Success = true, EntityId = entity.EntityId };
            }
            catch (Exception ex)
            {
                return new SpawnEntityResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<BuildStructureResult> BuildStructureAsync(BuildStructureRequest request)
        {
            if (_world == null)
                return new BuildStructureResult { Success = false, ErrorMessage = "World not initialized for this map" };

            try
            {
                // This is a placeholder - full implementation would require access to PrefabLibrary
                // For now, return success but log that it's not fully implemented
                Console.WriteLine($"[GameMapGrain] BuildStructureAsync called for {request.PrefabId} at ({request.X}, {request.Y}, {request.Z}), but requires PrefabLibrary integration");
                return new BuildStructureResult { Success = false, ErrorMessage = "BuildStructureAsync not fully implemented - requires PrefabLibrary" };
            }
            catch (Exception ex)
            {
                return new BuildStructureResult { Success = false, ErrorMessage = ex.Message };
            }
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


