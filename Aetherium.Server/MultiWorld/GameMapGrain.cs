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

        // Tracks spawn locations currently in use by joined players so JoinPlayerAsync
        // can hand out distinct cells. Not persisted — phase 1 player sessions are
        // ephemeral; clients reconnect with fresh sessions if the silo restarts.
        private readonly HashSet<Aetherium.Components.WorldLocation> _spawnsInUse = new();
        private readonly Dictionary<string, Aetherium.Components.WorldLocation> _playerSpawns = new();

        // Monotonic sequence number for emitted MapDeltas. Lets clients detect
        // gaps in delivery and trigger a resync (phase 2 doesn't implement the
        // resync mechanism yet; the sequence number is the wire-format hook).
        private long _nextSequence = 1;

        // Heat trail tracker — grain-authoritative per design D9. Heat is "objective
        // reality of the world" — a recently-walked cell is hot regardless of who's
        // looking. Sessions maintain a delta-driven local mirror.
        private readonly Aetherium.Server.Perception.HeatTrailTracker _heatTracker = new();

        // Subscription handle for _world.WorldEvents. Captured so OnDeactivate can detach.
        private Action<WorldEvent>? _worldEventsSubscriber;

        // Cached server-side session manager for delta application. Phase 2c routes
        // deltas through the host (which iterates affected sessions, applies the
        // delta to each local mirror, and ships fresh perceptions) rather than
        // direct SignalR-group fan-out — this preserves the perception-pure design
        // by ensuring clients only ever see filtered PerceptionDtos, not raw
        // deltas that could carry cells outside their FOV.
        private Aetherium.Server.GameSessionManager? _sessionManager;
        private bool _sessionManagerResolved;

        private Aetherium.Server.GameSessionManager? GetSessionManager()
        {
            if (!_sessionManagerResolved)
            {
                _sessionManager = this.ServiceProvider.GetService(
                    typeof(Aetherium.Server.GameSessionManager))
                    as Aetherium.Server.GameSessionManager;
                _sessionManagerResolved = true;
            }
            return _sessionManager;
        }

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
            if (_mapState.State != null && _mapState.State.Recipe != null && _world == null)
            {
                // Reactivation after silo restart: rehydrate _world by replaying the
                // captured recipe. Cheap (milliseconds) and avoids needing real World
                // serialization in phase 1. Mutation state (doors opened mid-game,
                // monsters spawned by ticks, etc.) is lost on restart — phase 2 will
                // address that via real snapshots in MapState.SerializedWorld.
                _world = RegenerateFromRecipe(_mapState.State.Recipe);
                await PartitionIntoRegionsAsync();
                AttachWorldEventSubscriber();
            }

            await base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Subscribes the grain to its <c>_world.WorldEvents</c> stream. Currently the
        /// subscriber translates <see cref="WorldEventType.EntityMoved"/> events for
        /// entities carrying a <see cref="Aetherium.Components.HeatSignature"/> into
        /// heat trail records and emits <see cref="HeatRecordedDelta"/>. Other event
        /// types pass through silently — their corresponding deltas (door state, item
        /// transfers, etc.) are emitted directly from the mutation methods that have
        /// the necessary context.
        /// </summary>
        private void AttachWorldEventSubscriber()
        {
            if (_world is null || _worldEventsSubscriber is not null)
                return;

            _worldEventsSubscriber = OnWorldEvent;
            _world.WorldEvents += _worldEventsSubscriber;
        }

        private void OnWorldEvent(WorldEvent evt)
        {
            try
            {
                if (evt.EventType != WorldEventType.EntityMoved || evt.Entity is null)
                    return;

                var heatSig = evt.Entity.Get<Aetherium.Components.HeatSignature>();
                if (heatSig is null || heatSig.Intensity <= 0.0)
                    return;

                var location = evt.Location;
                var clock = ServiceProvider.GetService(typeof(Aetherium.Server.Simulation.WorldClock))
                    as Aetherium.Server.Simulation.WorldClock;
                var gameTime = clock is not null
                    ? DateTime.UtcNow.AddHours(clock.GetTotalGameTimeHours() - (DateTime.UtcNow - DateTime.UnixEpoch).TotalHours)
                    : DateTime.UtcNow;

                _heatTracker.RecordEntityPosition(evt.Entity, location, gameTime);

                // Fire-and-forget delta emission — we're inside a synchronous event
                // callback so we can't await. The host-side broker doesn't depend on
                // ordering between deltas; each session reconciles independently.
                _ = FanOutAsync(new HeatRecordedDelta
                {
                    EntityId = evt.Entity.EntityId,
                    X = location.X,
                    Y = location.Y,
                    Z = location.Z,
                    GameTimeHours = clock?.GetTotalGameTimeHours() ?? 0,
                    Intensity = heatSig.Intensity,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] OnWorldEvent failed: {ex.Message}");
            }
        }

        private World RegenerateFromRecipe(WorldRecipe recipe)
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = recipe.GeneratorType,
                Width = recipe.Width,
                Height = recipe.Height,
                Levels = recipe.Levels,
                Seed = recipe.Seed,
                GeneratorVersion = recipe.GeneratorVersion,
                Template = recipe.Template,
                Parameters = new Dictionary<string, string>(recipe.Parameters, System.StringComparer.OrdinalIgnoreCase),
            };
            var orchestrator = new WorldGenerationOrchestrator(_generatorRegistry, BuildPasses(request.Template));
            var result = orchestrator.Generate(request);
            if (!result.Success || result.World is null)
                throw new InvalidOperationException("Failed to regenerate world from recipe on reactivation");
            return result.World;
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

            // Attach the WorldEvents subscriber that translates EntityMoved (for
            // heat-signature entities) into HeatRecordedDelta fan-out. Must happen
            // after _world is assigned but before any other code triggers movement.
            AttachWorldEventSubscriber();

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

            // Capture the recipe that produced this world so we can serve deterministic
            // snapshots later (GetWorldSnapshotAsync) and rehydrate on grain reactivation
            // (OnActivateAsync). Phase 1 persistence story; phase 2 will replace this
            // with real World serialization.
            var recipe = new WorldRecipe
            {
                GeneratorType = generatorType,
                Seed = seed,
                Parameters = new Dictionary<string, string>(parameterStrings, System.StringComparer.OrdinalIgnoreCase),
                Template = request.Template,
                GeneratorVersion = request.GeneratorVersion,
                Width = size.Width,
                Height = size.Height,
                Levels = size.Depth,
            };

            _mapState.State = new MapState
            {
                MapId = mapId,
                WorldId = worldId,
                MapName = mapName,
                Size = size,
                GeneratorType = generatorType,
                PlayerIds = new HashSet<string>(),
                CreatedAt = DateTime.UtcNow,
                Recipe = recipe,
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

        public async Task<JoinMapResult> JoinPlayerAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                return JoinMapResult.Fail("playerId required");

            if (_mapState.State is null || _world is null)
                return JoinMapResult.Fail("Map not initialized");

            if (_mapState.State.PlayerIds.Contains(playerId))
                return JoinMapResult.Fail("Player already on map");

            var spawn = SelectUnusedSpawn();
            if (spawn is null)
                return JoinMapResult.Fail("No passable spawn locations available");

            _spawnsInUse.Add(spawn);
            _playerSpawns[playerId] = spawn;
            _mapState.State.PlayerIds.Add(playerId);
            await _mapState.WriteStateAsync();

            // Phase 2: add the player's Character to _world so other joiners see them
            // and the grain owns canonical heading/inventory state. EntityId == playerId
            // keeps the mapping from sessionId to in-world entity trivially direct.
            var character = new Character { EntityId = playerId };
            character.Set(new Aetherium.Components.WorldLocation(spawn.X, spawn.Y, spawn.Z));
            character.Set(new Aetherium.Components.Inventory());
            character.Set(new Aetherium.Components.HasHeading { Heading = 0 });
            _world.AddEntity(character);

            // Fan out an EntityAddedDelta so other sessions in the map group see the
            // joiner appear. The joiner themselves will skip this when reconciling
            // because their snapshot omitted their own Character — but it's harmless
            // either way (AddEntity on their mirror would reject duplicate ID).
            await FanOutAsync(new EntityAddedDelta
            {
                MapId = _mapState.State.MapId,
                Placement = EntityPlacement.FromLocation(playerId, nameof(Character), spawn),
            });

            return JoinMapResult.Ok(_mapState.State.MapId, spawn, playerId);
        }

        public async Task LeavePlayerAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || _world is null || _mapState.State is null)
                return;

            // Remove from grain bookkeeping (mirrors RemovePlayerAsync but also
            // strips the Character entity and emits a delta for observers).
            _mapState.State.PlayerIds.Remove(sessionId);
            if (_playerSpawns.Remove(sessionId, out var spawn))
                _spawnsInUse.Remove(spawn);

            // Take the character's last known location for the delta before removing.
            int x = 0, y = 0, z = 0;
            if (_world.Entities.TryGetValue(sessionId, out var entity))
            {
                var loc = entity.Get<Aetherium.Components.WorldLocation>();
                if (loc is not null) { x = loc.X; y = loc.Y; z = loc.Z; }
                _world.TryRemoveEntity(sessionId);
            }

            await _mapState.WriteStateAsync();

            await FanOutAsync(new EntityRemovedDelta
            {
                MapId = _mapState.State.MapId,
                EntityId = sessionId,
                LastX = x, LastY = y, LastZ = z,
            });
        }

        public Task<WorldSnapshot> GetWorldSnapshotForJoinerAsync(string joinerPlayerId)
        {
            if (_mapState.State is null || _world is null || _mapState.State.Recipe is null)
                throw new InvalidOperationException("Map not initialized");

            var snapshot = WorldSnapshotBuilder.SnapshotOf(
                _world,
                _mapState.State.Recipe,
                _mapState.State.WorldId,
                _mapState.State.MapId,
                _mapState.State.Size,
                excludePlayerEntityId: joinerPlayerId);

            return Task.FromResult(snapshot);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync()
        {
            if (_mapState.State is null || _world is null || _mapState.State.Recipe is null)
                throw new InvalidOperationException("Map not initialized");

            var snapshot = WorldSnapshotBuilder.SnapshotOf(
                _world,
                _mapState.State.Recipe,
                _mapState.State.WorldId,
                _mapState.State.MapId,
                _mapState.State.Size);

            return Task.FromResult(snapshot);
        }

        /// <summary>
        /// Picks a passable location not currently held by another joined player.
        /// Walks the passable set once rather than rejection-sampling, so worlds with
        /// nearly-saturated spawn space don't loop forever.
        /// </summary>
        private Aetherium.Components.WorldLocation? SelectUnusedSpawn()
        {
            if (_world is null)
                return null;

            // World.SelectRandomPassableLocation builds its candidate list internally.
            // For up to a handful of joiners we can just retry; for many joiners we'd
            // prefer to enumerate once. Phase 1 keeps it simple.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var candidate = _world.SelectRandomPassableLocation();
                if (candidate is null)
                    return null;
                if (!_spawnsInUse.Contains(candidate))
                    return candidate;
            }

            // Fall back to exhaustive scan if random retries can't find a free cell.
            foreach (var loc in _world.EntitiesByLocation.Keys)
            {
                if (_world.PassableTerrain(loc) && !_spawnsInUse.Contains(loc))
                    return loc;
            }

            return null;
        }

        public async Task RemovePlayerAsync(string playerId)
        {
            if (_mapState.State == null)
                return;

            bool removed = _mapState.State.PlayerIds.Remove(playerId);

            // Free the spawn so a future joiner can reuse it.
            if (_playerSpawns.Remove(playerId, out var spawn))
                _spawnsInUse.Remove(spawn);

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

            // Heat trail cleanup. Capture which cells had trails before cleanup, run
            // cleanup, then diff to find cells that just lost their last trail. Emit
            // HeatExpiredDelta only for those — don't spam expiries every tick for
            // already-empty cells.
            try
            {
                var clock = ServiceProvider.GetService(typeof(Aetherium.Server.Simulation.WorldClock))
                    as Aetherium.Server.Simulation.WorldClock;
                var cutoff = DateTime.UtcNow.AddSeconds(-60);

                var beforeCells = new HashSet<Aetherium.Components.WorldLocation>(_heatTracker.SnapshotCounts().Keys);
                _heatTracker.CleanupOldTrails(cutoff);
                var afterCells = new HashSet<Aetherium.Components.WorldLocation>(_heatTracker.SnapshotCounts().Keys);

                var expired = beforeCells.Except(afterCells).ToList();
                foreach (var loc in expired)
                {
                    await FanOutAsync(new HeatExpiredDelta
                    {
                        X = loc.X,
                        Y = loc.Y,
                        Z = loc.Z,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] Heat cleanup failed: {ex.Message}");
            }
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

        // ==================================================================
        // Phase 2c — grain mutation methods.
        // Each method mutates _world directly and emits a MapDelta to the map's
        // SignalR group for fan-out to all sessions.
        // ==================================================================

        /// <summary>
        /// Looks up the player Character entity for a given session id (which is
        /// the player's entity id in our model). Returns null with reason if not
        /// found — caller maps to an appropriate failure result.
        /// </summary>
        private Character? GetPlayerCharacter(string sessionId)
        {
            if (_world is null) return null;
            return _world.Entities.TryGetValue(sessionId, out var entity)
                ? entity as Character
                : null;
        }

        /// <summary>
        /// Routes a MapDelta through the host-side session manager. The manager
        /// applies the delta to every session bound to this map, then pushes a
        /// fresh perception update over SignalR. Clients only ever see the
        /// resulting PerceptionDto — never raw deltas — so cells outside their
        /// FOV never reach the wire (perception-pure principle).
        /// </summary>
        private async Task FanOutAsync(MapDelta delta)
        {
            if (_mapState.State is null) return;
            delta.MapId = _mapState.State.MapId;
            delta.Sequence = System.Threading.Interlocked.Increment(ref _nextSequence);

            var mgr = GetSessionManager();
            if (mgr is null) return; // not wired (TestingHost path) — no consumers, no problem

            try
            {
                await mgr.NotifyMapMutationAsync(_mapState.State.MapId, delta);
            }
            catch (Exception ex)
            {
                // Mutation has already happened; a host-side failure must not roll it back.
                Console.WriteLine($"[GameMapGrain] FanOut failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an actor-only delta — used for state changes that observers
        /// don't perceive (e.g. heading changes). Goes only to the originating
        /// session's local mirror via the session manager's targeted path.
        /// </summary>
        private async Task SendToActorAsync(string sessionId, MapDelta delta)
        {
            if (_mapState.State is null) return;
            delta.MapId = _mapState.State.MapId;
            delta.Sequence = System.Threading.Interlocked.Increment(ref _nextSequence);

            var mgr = GetSessionManager();
            if (mgr is null) return;

            try
            {
                await mgr.NotifySessionMutationAsync(sessionId, delta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameMapGrain] SendToActor failed for {sessionId}: {ex.Message}");
            }
        }

        public async Task<MoveResult> MoveAsync(string sessionId, Aetherium.Model.RelativeDirection direction, int distance)
        {
            if (_world is null || _mapState.State is null) return MoveResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return MoveResult.Fail("Player not on map");

            var current = player.Get<Aetherium.Components.WorldLocation>();
            if (current is null) return MoveResult.Fail("Player has no location");

            // Translate relative direction against the player's current heading.
            var heading = player.Get<Aetherium.Components.HasHeading>()?.Heading ?? 0;
            var bearing = DegreesToCardinal(heading);
            var rotated = RotateRelativeByHeading(direction, bearing);

            var dest = rotated switch
            {
                Aetherium.WorldDirection.North => current.FromDelta(0, -distance, 0),
                Aetherium.WorldDirection.East  => current.FromDelta(+distance, 0, 0),
                Aetherium.WorldDirection.South => current.FromDelta(0, +distance, 0),
                Aetherium.WorldDirection.West  => current.FromDelta(-distance, 0, 0),
                _ => current,
            };

            // Preserve old position for the delta before _world.MoveEntity mutates the entity.
            var oldX = current.X; var oldY = current.Y; var oldZ = current.Z;

            _world.MoveEntity(player.EntityId, dest);

            await FanOutAsync(new EntityMovedDelta
            {
                EntityId = player.EntityId,
                OldX = oldX, OldY = oldY, OldZ = oldZ,
                NewX = dest.X, NewY = dest.Y, NewZ = dest.Z,
            });

            return MoveResult.Ok();
        }

        public async Task<RotateResult> RotateAsync(string sessionId, int degrees)
        {
            if (_world is null) return RotateResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return RotateResult.Fail("Player not on map");

            var heading = player.Get<Aetherium.Components.HasHeading>();
            if (heading is null) return RotateResult.Fail("Player has no heading component");

            heading.Heading += degrees;

            // Heading is private information per the perception-pure design — send
            // the delta only to the actor's own session. Other players' clients
            // never see another character's facing direction (until a future change
            // adds compass-style perception filters).
            await SendToActorAsync(sessionId, new EntityHeadingChangedDelta
            {
                EntityId = player.EntityId,
                Degrees = heading.Heading,
            });

            return RotateResult.Ok(heading.Heading);
        }

        public async Task<ChangeLevelResult> ChangeLevelAsync(string sessionId, int deltaZ)
        {
            if (_world is null) return ChangeLevelResult.Fail("Map not initialized");
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return ChangeLevelResult.Fail("Player not on map");

            var current = player.Get<Aetherium.Components.WorldLocation>();
            if (current is null) return ChangeLevelResult.Fail("Player has no location");

            var dest = current.FromDelta(0, 0, deltaZ);
            var oldX = current.X; var oldY = current.Y; var oldZ = current.Z;

            _world.MoveEntity(player.EntityId, dest);

            await FanOutAsync(new EntityMovedDelta
            {
                EntityId = player.EntityId,
                OldX = oldX, OldY = oldY, OldZ = oldZ,
                NewX = dest.X, NewY = dest.Y, NewZ = dest.Z,
            });

            return ChangeLevelResult.Ok(dest.Z);
        }

        // ------------------------------------------------------------------
        // Pickup / Drop / Open / Close — delegate to InteractionSystem's
        // ActionContext overloads (refactor-interaction-system-stateless).
        // The grain captures the post-condition context needed to emit the
        // appropriate delta after a successful interaction. The mutation
        // logic itself lives in InteractionSystem, shared with the legacy
        // session-based code path used by LocalMutationGateway.
        // ------------------------------------------------------------------

        private readonly InteractionSystem _interactionSystem = new InteractionSystem();

        private ActionContext? TryBuildActionContext(string sessionId)
        {
            if (_world is null) return null;
            var player = GetPlayerCharacter(sessionId);
            if (player is null) return null;
            var loc = player.Get<Aetherium.Components.WorldLocation>();
            if (loc is null) return null;
            return new ActionContext(_world, player, loc);
        }

        public async Task<Aetherium.Model.InteractionResultDto> PickupAsync(string sessionId, string targetEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");

            // Capture target metadata BEFORE the interaction removes the entity from
            // _world. Needed to build the post-success ItemTransferredDelta.
            _world!.Entities.TryGetValue(targetEntityId, out var target);
            var targetType = target?.GetType().Name ?? string.Empty;

            var result = _interactionSystem.TryPickup(ctx, targetEntityId);
            if (!result.Success || target is null)
                return new Aetherium.Model.InteractionResultDto { Success = result.Success, Reason = result.Reason };

            var placement = EntityPlacement.FromLocation(targetEntityId, targetType, ctx.ViewLocation);
            EntityFactory.ExtractProperties(target, placement);

            await FanOutAsync(new ItemTransferredDelta
            {
                ItemEntityId = targetEntityId,
                IntoInventory = true,
                OwnerEntityId = ctx.Player.EntityId,
                X = ctx.ViewLocation.X, Y = ctx.ViewLocation.Y, Z = ctx.ViewLocation.Z,
                ItemPlacement = placement,
            });

            return Ok();
        }

        public async Task<Aetherium.Model.InteractionResultDto> DropAsync(string sessionId, string itemEntityId)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");

            var result = _interactionSystem.TryDrop(ctx, itemEntityId);
            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            await FanOutAsync(new ItemTransferredDelta
            {
                ItemEntityId = itemEntityId,
                IntoInventory = false,
                OwnerEntityId = ctx.Player.EntityId,
                X = ctx.ViewLocation.X, Y = ctx.ViewLocation.Y, Z = ctx.ViewLocation.Z,
                ItemPlacement = null,
            });

            return Ok();
        }

        public async Task<Aetherium.Model.InteractionResultDto> OpenAsync(string sessionId, string targetEntityId)
            => await ToggleDoorAsync(sessionId, targetEntityId, wantOpen: true);

        public async Task<Aetherium.Model.InteractionResultDto> CloseAsync(string sessionId, string targetEntityId)
            => await ToggleDoorAsync(sessionId, targetEntityId, wantOpen: false);

        private async Task<Aetherium.Model.InteractionResultDto> ToggleDoorAsync(string sessionId, string targetEntityId, bool wantOpen)
        {
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");

            var result = wantOpen
                ? _interactionSystem.TryOpen(ctx, targetEntityId)
                : _interactionSystem.TryClose(ctx, targetEntityId);

            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            // The InteractionSystem mutated the OpensAndCloses component; read it back
            // for the delta. We already know the target exists because TryOpen/TryClose
            // returned success.
            var target = ctx.World.Entities[targetEntityId];
            var oc = target.Get<Aetherium.Components.OpensAndCloses>()!;

            await FanOutAsync(new DoorStateChangedDelta
            {
                EntityId = targetEntityId,
                IsOpen = oc.IsOpen,
                IsLocked = oc.IsLocked,
            });

            return Ok();
        }

        public async Task<Aetherium.Model.InteractionResultDto> UseAsync(string sessionId, string itemEntityId, string onEntityId, string? usageId)
        {
            // Full-fidelity Use via InteractionSystem. Snapshot the mutable fields
            // we care about, delegate the mutation, then diff and emit deltas.
            // See openspec/changes/extend-delta-vocabulary-for-use-disambiguation.
            var ctx = TryBuildActionContext(sessionId);
            if (ctx is null) return Fail("Map not initialized or player not on map");

            // Resolve item (must be in inventory). Target is optional for some
            // modes (consume, place) but required for others (unlock, lockpick,
            // force-open). InteractionSystem handles the per-mode requirement.
            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            if (inv is null || !inv.Items.TryGetValue(itemEntityId, out var item))
                return Fail("Item not in inventory");

            ctx.World.Entities.TryGetValue(onEntityId, out var target);

            // Reactive disambiguation when usageId is omitted.
            string resolvedUsageId;
            if (string.IsNullOrEmpty(usageId))
            {
                var options = _interactionSystem.GetUseOptions(ctx, itemEntityId, target is null ? null : onEntityId);
                if (options.Count == 0) return Fail("No effect");
                if (options.Count > 1)
                {
                    // Return options for the client to pick from.
                    return new Aetherium.Model.InteractionResultDto
                    {
                        Success = false,
                        Reason = "Multiple usage options available",
                        Options = options.Select(o => new Aetherium.Model.UsageOptionDto
                        {
                            UsageId = o.UsageId,
                            Label = o.Label,
                            Description = o.Description,
                        }).ToList()
                    };
                }
                resolvedUsageId = options[0].UsageId;
            }
            else
            {
                resolvedUsageId = usageId;
            }

            // Snapshot all fields the dispatcher could mutate. We track them on
            // the item, the target (if a door), and the player. Inventory
            // membership is tracked separately so we can detect destruction
            // (item leaves inventory and is NOT in the world) vs placement
            // (item leaves inventory and IS in the world).
            var snapshot = SnapshotUseFields(ctx, item, target);

            var result = _interactionSystem.TryUseWithMode(ctx, itemEntityId, target is null ? null : onEntityId, resolvedUsageId);
            if (!result.Success)
                return new Aetherium.Model.InteractionResultDto { Success = false, Reason = result.Reason };

            await EmitUseDeltasAsync(ctx, item, target, snapshot);
            return Ok();
        }

        /// <summary>
        /// Component-field values captured before a Use action so the grain can
        /// diff the post-state and emit precise deltas. Single-purpose record;
        /// not used outside UseAsync.
        /// </summary>
        private sealed class UseFieldSnapshot
        {
            public int? ConsumableUses;
            public int? HealthLevel;
            public int? ForcesDoorDurability;
            public int? LockpickDurability;
            public bool? PlaceableLightIsPlaced;
            public bool? LightSourceIsEnabled;
            public bool? LightSourceIsDynamic;
            public bool? ActivatableIsActivated;
            public int? InventoryCapacity;

            public bool? DoorIsOpen;
            public bool? DoorIsLocked;

            public bool ItemWasInInventory;
            public bool ItemWasInWorld;
        }

        private static UseFieldSnapshot SnapshotUseFields(ActionContext ctx, Aetherium.Core.Entity item, Aetherium.Core.Entity? target)
        {
            var s = new UseFieldSnapshot();

            s.ConsumableUses = item.Get<Aetherium.Components.Consumable>()?.Uses;
            s.ForcesDoorDurability = item.Get<Aetherium.Components.ForcesDoor>()?.Durability;
            s.LockpickDurability = item.Get<Aetherium.Components.Lockpick>()?.Durability;
            s.PlaceableLightIsPlaced = item.Get<Aetherium.Components.PlaceableLight>()?.IsPlaced;
            var ls = item.Get<Aetherium.Components.LightSource>();
            s.LightSourceIsEnabled = ls?.IsEnabled;
            s.LightSourceIsDynamic = ls?.IsDynamic;

            s.HealthLevel = ctx.Player.Get<Aetherium.Components.Health>()?.Level;
            s.InventoryCapacity = ctx.Player.Get<Aetherium.Components.Inventory>()?.Capacity;

            if (target is not null)
            {
                var door = target.Get<Aetherium.Components.OpensAndCloses>();
                s.DoorIsOpen = door?.IsOpen;
                s.DoorIsLocked = door?.IsLocked;
                s.ActivatableIsActivated = target.Get<Aetherium.Components.Activatable>()?.IsActivated;
            }

            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            s.ItemWasInInventory = inv is not null && inv.Items.ContainsKey(item.EntityId);
            s.ItemWasInWorld = ctx.World.Entities.ContainsKey(item.EntityId);

            return s;
        }

        /// <summary>
        /// Diffs the post-Use state against the pre-call snapshot and emits one
        /// delta per changed field, plus the appropriate inventory/world
        /// transition delta if the item moved.
        /// </summary>
        private async Task EmitUseDeltasAsync(ActionContext ctx, Aetherium.Core.Entity item, Aetherium.Core.Entity? target, UseFieldSnapshot snapshot)
        {
            var inv = ctx.Player.Get<Aetherium.Components.Inventory>();
            var itemIsInInventory = inv is not null && inv.Items.ContainsKey(item.EntityId);
            var itemIsInWorld = ctx.World.Entities.ContainsKey(item.EntityId);

            // Inventory transitions first — the field deltas below need to refer
            // to an item the receiver can still find.
            if (snapshot.ItemWasInInventory && !itemIsInInventory)
            {
                if (itemIsInWorld)
                {
                    // Placement: item went from inventory to world (TryPlace).
                    var placement = EntityPlacement.FromLocation(item.EntityId, item.GetType().Name, ctx.ViewLocation);
                    await FanOutAsync(new EntityPlacedDelta
                    {
                        Placement = placement,
                        SourceOwnerEntityId = ctx.Player.EntityId,
                    });
                }
                else
                {
                    // Destruction: item left inventory and didn't enter the world
                    // (Consumable at zero uses, broken Lockpick, broken ForcesDoor).
                    await FanOutAsync(new ItemDestroyedDelta
                    {
                        EntityId = item.EntityId,
                        OwnerEntityId = ctx.Player.EntityId,
                    });
                }
            }

            // Component-field deltas. Door state has its own delta type because
            // it bundles IsOpen+IsLocked; everything else goes through the
            // generic ComponentFieldChangedDelta.
            if (target is not null)
            {
                var door = target.Get<Aetherium.Components.OpensAndCloses>();
                if (door is not null && (door.IsOpen != snapshot.DoorIsOpen || door.IsLocked != snapshot.DoorIsLocked))
                {
                    await FanOutAsync(new DoorStateChangedDelta
                    {
                        EntityId = target.EntityId,
                        IsOpen = door.IsOpen,
                        IsLocked = door.IsLocked,
                    });
                }

                var activatable = target.Get<Aetherium.Components.Activatable>();
                if (activatable is not null && activatable.IsActivated != snapshot.ActivatableIsActivated)
                {
                    await FanOutAsync(BoolFieldDelta(target.EntityId, "Activatable", "IsActivated", activatable.IsActivated));
                }
            }

            // Item-side fields. Skip if item was destroyed (ItemDestroyedDelta is enough).
            if (itemIsInInventory || itemIsInWorld)
            {
                var consumable = item.Get<Aetherium.Components.Consumable>();
                if (consumable is not null && consumable.Uses != snapshot.ConsumableUses)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "Consumable", "Uses", consumable.Uses));

                var forces = item.Get<Aetherium.Components.ForcesDoor>();
                if (forces is not null && forces.Durability != snapshot.ForcesDoorDurability)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "ForcesDoor", "Durability", forces.Durability));

                var lockpick = item.Get<Aetherium.Components.Lockpick>();
                if (lockpick is not null && lockpick.Durability != snapshot.LockpickDurability)
                    await FanOutAsync(IntFieldDelta(item.EntityId, "Lockpick", "Durability", lockpick.Durability));

                var placeable = item.Get<Aetherium.Components.PlaceableLight>();
                if (placeable is not null && placeable.IsPlaced != snapshot.PlaceableLightIsPlaced)
                    await FanOutAsync(BoolFieldDelta(item.EntityId, "PlaceableLight", "IsPlaced", placeable.IsPlaced));

                var ls = item.Get<Aetherium.Components.LightSource>();
                if (ls is not null)
                {
                    if (ls.IsEnabled != snapshot.LightSourceIsEnabled)
                        await FanOutAsync(BoolFieldDelta(item.EntityId, "LightSource", "IsEnabled", ls.IsEnabled));
                    if (ls.IsDynamic != snapshot.LightSourceIsDynamic)
                        await FanOutAsync(BoolFieldDelta(item.EntityId, "LightSource", "IsDynamic", ls.IsDynamic));
                }
            }

            // Player-side fields.
            var health = ctx.Player.Get<Aetherium.Components.Health>();
            if (health is not null && health.Level != snapshot.HealthLevel)
                await FanOutAsync(IntFieldDelta(ctx.Player.EntityId, "Health", "Level", health.Level));

            var playerInv = ctx.Player.Get<Aetherium.Components.Inventory>();
            if (playerInv is not null && playerInv.Capacity != snapshot.InventoryCapacity)
                await FanOutAsync(IntFieldDelta(ctx.Player.EntityId, "Inventory", "Capacity", playerInv.Capacity));
        }

        private static ComponentFieldChangedDelta IntFieldDelta(string entityId, string componentType, string fieldName, int value)
            => new ComponentFieldChangedDelta
            {
                EntityId = entityId,
                ComponentType = componentType,
                FieldName = fieldName,
                NumericValue = value,
            };

        private static ComponentFieldChangedDelta BoolFieldDelta(string entityId, string componentType, string fieldName, bool value)
            => new ComponentFieldChangedDelta
            {
                EntityId = entityId,
                ComponentType = componentType,
                FieldName = fieldName,
                BoolValue = value,
            };

        // ----- helpers ---------------------------------------------------

        private static Aetherium.Model.InteractionResultDto Ok()
            => new Aetherium.Model.InteractionResultDto { Success = true };

        private static Aetherium.Model.InteractionResultDto Fail(string reason)
            => new Aetherium.Model.InteractionResultDto { Success = false, Reason = reason };

        private static Aetherium.WorldDirection DegreesToCardinal(int degrees)
        {
            int n = ((degrees % 360) + 360) % 360;
            if (n < 45 || n >= 315) return Aetherium.WorldDirection.North;
            if (n < 135) return Aetherium.WorldDirection.East;
            if (n < 225) return Aetherium.WorldDirection.South;
            return Aetherium.WorldDirection.West;
        }

        private static Aetherium.WorldDirection RotateRelativeByHeading(Aetherium.Model.RelativeDirection rel, Aetherium.WorldDirection heading)
        {
            // Translate (Forward/Backward/Left/Right) against the character's bearing.
            // Forward = heading; Backward = opposite; Left = 90° CCW; Right = 90° CW.
            int rotations = rel switch
            {
                Aetherium.Model.RelativeDirection.Forward => 0,
                Aetherium.Model.RelativeDirection.Right => 1,
                Aetherium.Model.RelativeDirection.Backward => 2,
                Aetherium.Model.RelativeDirection.Left => 3,
                _ => 0,
            };
            var d = heading;
            for (int i = 0; i < rotations; i++)
                d = d switch
                {
                    Aetherium.WorldDirection.North => Aetherium.WorldDirection.East,
                    Aetherium.WorldDirection.East => Aetherium.WorldDirection.South,
                    Aetherium.WorldDirection.South => Aetherium.WorldDirection.West,
                    Aetherium.WorldDirection.West => Aetherium.WorldDirection.North,
                    _ => d,
                };
            return d;
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

        /// <summary>
        /// Reserved for phase 2 real <c>World</c> serialization. Phase 1 uses
        /// <see cref="Recipe"/> + ephemeral mutation loss on restart.
        /// </summary>
        [Id(7)] public byte[]? SerializedWorld { get; set; }

        /// <summary>
        /// Recipe captured at <c>InitializeAsync</c> time so the grain can produce
        /// deterministic snapshots and rehydrate <c>_world</c> on reactivation.
        /// </summary>
        [Id(8)] public WorldRecipe? Recipe { get; set; }
    }
}


