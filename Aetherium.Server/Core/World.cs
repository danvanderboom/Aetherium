using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Aetherium;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.Core
{
    public class World
    {
        /// <summary>
        /// This world's tiling (docs/grid-topologies.md) — how cells connect, measure,
        /// and face. Resolved once from the per-world <c>topology</c> config at map-grain
        /// init; defaults to square, byte-identically to the pre-seam engine. All
        /// adjacency/distance/line/facing math must route through this, never inline
        /// ±1 offsets or Manhattan sums.
        /// </summary>
        public Aetherium.Topology.IGridTopology Topology { get; set; } = Aetherium.Topology.SquareTopology.Instance;

        public Dictionary<string, TileType> TileTypes { get; protected set; }
        public Dictionary<string, TerrainType> TerrainTypes { get; protected set; }

        public List<WorldFeature> Features { get; set; }

        public ConcurrentDictionary<string, Entity> Entities { get; set; }
        public ConcurrentDictionary<WorldLocation, ConcurrentDictionary<string, Entity>> EntitiesByLocation { get; set; }
        public ConcurrentDictionary<string, Character> Characters { get; set; }

        /// <summary>
        /// Per-world character-memory policy (defaults: enabled, capped, 1h decay half-life).
        /// Set from world generator parameters during map initialization.
        /// </summary>
        public MemoryPolicy MemoryPolicy { get; set; } = new MemoryPolicy();

        /// <summary>
        /// Per-world individual-recognition policy (add-identity-recognition; disabled by default).
        /// Set from world generator parameters during map initialization.
        /// </summary>
        public RecognitionPolicy RecognitionPolicy { get; set; } = new RecognitionPolicy();

        public Guid CharacterMoveTimestamp { get; protected set; } = Guid.NewGuid();

        // --- Altitude bands & layered obstruction (see docs/design/flying-entities.md) ---

        /// <summary>Lowest altitude band the world models (e.g. deep transit / subway).</summary>
        public int MinBand { get; set; } = -4;

        /// <summary>Highest altitude band the world models (e.g. skyway / orbit).</summary>
        public int MaxBand { get; set; } = 6;

        /// <summary>
        /// Obstruction height applied to impassable terrain that has no explicit "ObstructionHeight" setting.
        /// A wall/mountain blocks this many bands upward from its own band; flyers above it are clear.
        /// </summary>
        public int DefaultWallHeight { get; set; } = 3;

        /// <summary>Optional human-readable labels per band (e.g. -2 =&gt; "subway", 3 =&gt; "monorail").</summary>
        public Dictionary<int, string> BandLabels { get; set; } = new Dictionary<int, string>();

        // --- 3D occluded perception slab (see docs/design/adaptive-depth-visualization.md) ---

        /// <summary>How many bands below the viewer's band perception evaluates. 0 keeps perception single-Z.</summary>
        public int SlabDepthBelow { get; set; } = 0;

        /// <summary>How many bands above the viewer's band perception evaluates. 0 keeps perception single-Z.</summary>
        public int SlabDepthAbove { get; set; } = 0;

        /// <summary>Hard cap on slab depth per direction, so a misconfigured world can't make perception unbounded.</summary>
        public int SlabDepthCap { get; set; } = 8;

        /// <summary>Optional semicircular cruising-altitude rule used by flight-plan generators.</summary>
        public CruiseRule? CruiseRule { get; set; }

        /// <summary>How converging flyers/characters are resolved. Default preserves prior behavior.</summary>
        public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Separated;

        /// <summary>Terrain names a flyer with CanLand may land on / take off from (per-world data).</summary>
        public HashSet<string> LandingTerrainNames { get; set; } = new HashSet<string> { "Road", "Plains", "Landingpad" };

        public event Action<WorldEvent>? WorldEvents;

        public World()
        {
            TerrainTypes = new Dictionary<string, TerrainType>();
            TileTypes = new Dictionary<string, TileType>();

            Features = new List<WorldFeature>();

            Entities = new ConcurrentDictionary<string, Entity>();
            EntitiesByLocation = new ConcurrentDictionary<WorldLocation, ConcurrentDictionary<string, Entity>>();
            Characters = new ConcurrentDictionary<string, Character>();
        }

        public WorldLocation? SelectRandomPassableLocation()
        {
            // Build a snapshot of passable locations once, then sample. This avoids
            // an infinite loop when no locations are passable.
            var passable = EntitiesByLocation.Keys.Where(PassableTerrain).ToList();
            if (passable.Count == 0)
                return null;

            return passable[Random.Shared.Next(0, passable.Count)];
        }

        public T? SelectRandomEntity<T>(IList<string>? excludedEntityIds = null)
            where T : Entity
        {
            var exclusions = excludedEntityIds ?? new List<string>();

            var cs = Entities.Values.OfType<T>()
                .Where(e => !exclusions.Contains(e.EntityId))
                .ToList();

            if (cs.Count == 0)
                return null;

            return cs[Random.Shared.Next(0, cs.Count)];
        }

        public TerrainType? GetTerrainType(WorldLocation location)
        {
            var terrain = GetTerrain(location);
            if (terrain == null)
                return null;

            return TerrainTypes[terrain.Type.Name];
        }

        public Terrain? GetTerrain(WorldLocation location) =>
            !EntitiesByLocation.ContainsKey(location) ? null
            : EntitiesByLocation[location].Values
            .OfType<Terrain>()
            .FirstOrDefault();

        public List<Terrain> GetTerrain(WorldChunk chunk) =>
            GetTerrain(chunk.Location, chunk.Size);

        public List<Terrain> GetTerrain(WorldLocation location, Size3d size)
        {
            var results = new List<Terrain>();

            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        foreach (var terrainEntity in EntitiesByLocation[location.FromDelta(x, y, z)].Values.OfType<Terrain>())
                            results.Add(terrainEntity);

            return results;
        }

        public void SetTerrain(string name, WorldChunk chunk) =>
            SetTerrain(name, chunk.Location, chunk.Size);

        public void SetTerrain(string name, WorldLocation location, Size3d size)
        {
            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        SetTerrain(name, location.FromDelta(x, y, z));
        }

        public Terrain? SetTerrain(string name, WorldLocation location)
        {
            var terrainType = TerrainTypes[name];
            if (terrainType == null)
                throw new InvalidOperationException($"Terrain not registered: '{name}'");

            var terrain = GetTerrain(location);
            if (terrain == null)
            {
                terrain = new Terrain(terrainType);
                terrain.Set(new Tile { Type = terrainType.TileType ?? TileType.None });
                terrain.Set(location);
                AddEntity(terrain);
            }
            else
            {
                terrain.Type = terrainType;
                terrain.Set(new Tile { Type = terrainType.TileType ?? TileType.None });
            }

            return terrain;
        }

        public void Build()
        {
            foreach (var feature in Features)
                feature.FeatureBuilder?.Invoke(this, feature).Build();
        }

        public void AddTileTypes(IList<TileType> tileTypes)
        {
            foreach (var tileType in tileTypes)
                TileTypes.Add(tileType.Name, tileType);
        }

        public void AddTerrainTypes(IList<TerrainType> terrainTypes)
        {
            foreach (var terrainType in terrainTypes)
                TerrainTypes.Add(terrainType.Name, terrainType);
        }

        public void AddEntity(Entity entity)
        {
            if (Entities.TryAdd(entity.EntityId, entity))
            {
                ConcurrentDictionary<string, Entity> dict;

                if (EntitiesByLocation.TryGetValue(entity.Get<WorldLocation>(), out var existingDict))
                {
                    dict = existingDict;
                }
                else
                {
                    dict = new ConcurrentDictionary<string, Entity>();

                    if (!EntitiesByLocation.TryAdd(entity.Get<WorldLocation>(), dict))
                        throw new Exception("Failed to add to EntitiesByLocation");
                }

                if (dict.TryAdd(entity.EntityId, entity))
                {
                    var character = entity as Character;
                    if (character != null && !Characters.TryAdd(entity.EntityId, character))
                        throw new Exception("Couldn't add to Character index");

                    WorldEvents?.Invoke(new WorldEvent
                    {
                        EventType = WorldEventType.EntityAdded,
                        Location = entity.Get<WorldLocation>(),
                        Entity = entity
                    });
                }
                else
                {
                    throw new Exception("Couldn't add to EntitiesByLocation index");
                }
            }
            else
            {
                throw new Exception("Couldn't add to Entities index");
            }
        }

        public void RemoveEntity(string Id)
        {
            if (!TryRemoveEntity(Id))
                throw new ArgumentException($"EntityId not found: {Id}");
        }

        /// <summary>
        /// Atomically removes an entity. Returns true if this call won the race; false if the
        /// entity was already removed (or never existed). Use this when racing pickups must
        /// produce exactly one winner.
        /// </summary>
        public bool TryRemoveEntity(string Id)
        {
            if (!Entities.TryGetValue(Id, out var entity))
                return false;

            var location = entity.Get<WorldLocation>();
            if (!EntitiesByLocation.TryGetValue(location, out var entitiesAtLocation))
                return false;

            // The location-bucket remove is the single atomic step that decides the race.
            // Whoever flips this from "present" to "absent" wins; everyone else returns false.
            if (!entitiesAtLocation.TryRemove(Id, out var _))
                return false;

            Entities.TryRemove(Id, out var _);

            if (entity is Character)
                Characters.TryRemove(entity.EntityId, out var _);

            if (entitiesAtLocation.IsEmpty)
                EntitiesByLocation.TryRemove(location, out var _);

            WorldEvents?.Invoke(new WorldEvent
            {
                EventType = WorldEventType.EntityRemoved,
                Location = location,
                Entity = entity
            });

            return true;
        }

        public void MoveEntity(string Id, WorldLocation destination)
        {
            if (Entities.TryGetValue(Id, out var entity))
            {
                var source = entity.Get<WorldLocation>();
                if (source == destination)
                    return;

                // remove from location index
                if (EntitiesByLocation.TryGetValue(source, out var entitiesAtSource)
                    && entitiesAtSource.TryRemove(Id, out var _))
                {
                    // update location on entity
                    entity.Set(destination);

                    // Get-or-create the destination bucket so entities never leak into limbo when moving onto a
                    // cell that doesn't yet have an index entry (e.g. a newly-uncovered tile, a previously-empty
                    // air cell for an airborne flyer, or a cell whose only resident was just removed).
                    var entitiesAtDestination = EntitiesByLocation.GetOrAdd(
                        destination,
                        _ => new ConcurrentDictionary<string, Entity>());

                    // Index the entity at the destination unconditionally. A previous
                    // TryAdd here could fail (e.g. a concurrent double-move already put
                    // this id at the destination) *after* we removed it from the source
                    // bucket, dropping the entity from the location index entirely. The
                    // indexer is idempotent and always wins, so the entity is guaranteed
                    // to be indexed at exactly one place when MoveEntity returns.
                    entitiesAtDestination[Id] = entity;

                    // Clean up empty source bucket
                    if (entitiesAtSource.IsEmpty)
                        EntitiesByLocation.TryRemove(source, out var _);

                    WorldEvents?.Invoke(new WorldEvent
                    {
                        EventType = WorldEventType.EntityMoved,
                        Location = destination,
                        Entity = entity
                    });
                }
            }
        }

        public bool TryMove(Character character, WorldDirection direction)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                throw new InvalidOperationException("Character is missing WorldLocation");

            // Horizontal moves resolve through the topology (WorldDirection is
            // square-legacy cosmetics; its cardinal headings match the engine
            // convention North = -Y, South = +Y — the same as GameSession.MoveView
            // and the rest of the perception stack). Vertical stays engine-level.
            switch (direction)
            {
                case WorldDirection.North:
                case WorldDirection.South:
                case WorldDirection.West:
                case WorldDirection.East:
                    return TryMove(character, StepToward(location, CardinalDegrees(direction)));
                case WorldDirection.Up:
                    return TryMove(character, location.FromDelta(0, 0, +1));
                case WorldDirection.Down:
                    return TryMove(character, location.FromDelta(0, 0, -1));
                default:
                    return false;
            }
        }

        /// <summary>The heading, in degrees, of a horizontal <see cref="WorldDirection"/>.
        /// Degrees are the engine-wide facing source of truth; the enum is square-legacy.</summary>
        private static int CardinalDegrees(WorldDirection direction) => direction switch
        {
            WorldDirection.North => 0,
            WorldDirection.East => 90,
            WorldDirection.South => 180,
            WorldDirection.West => 270,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a horizontal direction"),
        };

        /// <summary>The neighbor across the outgoing edge nearest <paramref name="headingDegrees"/>.</summary>
        private WorldLocation StepToward(WorldLocation location, int headingDegrees)
        {
            var cell = Aetherium.Topology.GridCoord.From(location);
            var index = Topology.HeadingToDirectionIndex(cell, headingDegrees);
            return index is null ? location : Topology.GetStep(cell, index.Value).Target.ToWorldLocation();
        }

        public bool TryMove(Character character, WorldLocation location)
        {
            var isAirborne = character.Has<Flight>()
                && character.Get<Flight>().State == FlightState.Airborne;

            // Grounded moves require a terrain-bearing cell; airborne flyers may enter open air.
            if (!isAirborne && !EntitiesByLocation.ContainsKey(location))
                return false;

            // stop players (including monsters) from existing in the same location.
            // Iterate the Characters index (not all Entities) so terrain entities don't inflate the scan, and
            // exclude the character itself so a no-op move doesn't trigger self-collision damage.
            var other = Characters.Values
                .FirstOrDefault(p => p.EntityId != character.EntityId && p.Get<WorldLocation>() == location);
            if (other != null)
            {
                if (CollisionPolicy == CollisionPolicy.Collidable)
                {
                    WorldEvents?.Invoke(new WorldEvent
                    {
                        EventType = WorldEventType.Collision,
                        Location = location,
                        Entity = character
                    });
                }

                var health = character.Get<Health>();
                if (health != null)
                {
                    if (health.Level > 0)
                        health.Level--;

                    //if (health.Level == 0)
                    //    CharacterDied?.Invoke(character);

                    return false;
                }
            }

            var currentLocation = character.Get<WorldLocation>();
            if (currentLocation == null)
                return false;

            // Vertical gating (stairs/ladders) applies to grounded entities only. Airborne flyers change
            // altitude freely; the [MinBand, MaxBand] limit is enforced by IsPassable.
            if (!isAirborne)
            {
                var up = currentLocation.FromDelta(0, 0, +1);
                var down = currentLocation.FromDelta(0, 0, -1);

                if (location == up && !currentLocation.Has<CanAscend>())
                    return false;
                else if (location == down && !currentLocation.Has<CanDescend>())
                    return false;
            }

            if (IsPassable(location, character))
            {
                MoveEntity(character.EntityId, location);

                CharacterMoveTimestamp = Guid.NewGuid();

                return true;
            }

            return false;
        }

        // ==================================================================
        // Validated movement (the single authoritative movement rule).
        //
        // Every live movement path — GameSession.MoveView/ChangeLevel (legacy
        // sessions and LocalMutationGateway), GameMapGrain.MoveAsync/
        // ChangeLevelAsync (grain-hosted maps and GrainMutationGateway), and
        // GameManagementGrain.MoveAsync — routes through TryMoveSteps /
        // TryChangeLevel below, so a client can never walk through walls,
        // closed doors, other characters, or off the generated map, regardless
        // of which protocol path the request arrived on. The older TryMove
        // above predates this API and is kept for its existing tests; do not
        // add new callers to it.
        // ==================================================================

        /// <summary>
        /// True when a character could legally stand at <paramref name="location"/>:
        /// the cell exists, its terrain is passable, and no obstructing entity or
        /// character occupies it. Location-only variant of <see cref="MovementBlocker"/>
        /// for spawn placement, where the character isn't in the world yet.
        /// </summary>
        public bool IsOpenForOccupancy(WorldLocation location)
        {
            if (!EntitiesByLocation.TryGetValue(location, out var atLocation))
                return false;

            if (!PassableTerrain(location))
                return false;

            foreach (var entity in atLocation.Values)
            {
                if (entity.AllComponents.OfType<ObstructsMovement>().Any(o => o.Obstruction > 0))
                    return false;

                if (entity is Character)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns null when <paramref name="character"/> may enter
        /// <paramref name="destination"/>, otherwise a human-readable reason.
        /// Rules: the destination must be a cell the world knows about (no
        /// walking into the void), its terrain must be passable, and it must
        /// not contain a movement-obstructing entity (wall entity, closed
        /// door, window, portcullis) or another character.
        /// </summary>
        public string? MovementBlocker(Character character, WorldLocation destination)
        {
            if (!EntitiesByLocation.TryGetValue(destination, out var atDestination))
                return "There is nothing there";

            if (!PassableTerrain(destination))
                return "Blocked by terrain";

            foreach (var entity in atDestination.Values)
            {
                if (entity.EntityId == character.EntityId)
                    continue;

                if (entity.AllComponents.OfType<ObstructsMovement>().Any(o => o.Obstruction > 0))
                    return "The way is blocked";

                if (entity is Character)
                    return "Someone is in the way";
            }

            return null;
        }

        /// <summary>
        /// Moves <paramref name="character"/> up to <paramref name="distance"/>
        /// cells in a cardinal direction, validating every step via
        /// <see cref="MovementBlocker"/>. Stops at the first blocked cell, so a
        /// multi-cell request can succeed partially — check
        /// <see cref="MoveOutcome.StepsTaken"/> and <see cref="MoveOutcome.BlockedReason"/>.
        /// </summary>
        public MoveOutcome TryMoveSteps(Character character, WorldDirection direction, int distance)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                return MoveOutcome.Blocked(null, "Character has no location");

            if (distance < 1)
                return MoveOutcome.Blocked(location, "Distance must be at least 1");

            if (direction is not (WorldDirection.North or WorldDirection.South
                or WorldDirection.East or WorldDirection.West))
                return MoveOutcome.Blocked(location, "Vertical movement goes through TryChangeLevel");

            // A cardinal move is "face that heading, step forward" — on square this is
            // the exact (dx, dy) table this method used to inline.
            return TryMoveSteps(character, CardinalDegrees(direction), Aetherium.Model.RelativeDirection.Forward, distance);
        }

        /// <summary>
        /// Moves <paramref name="character"/> up to <paramref name="distance"/> steps by
        /// resolving <paramref name="move"/> against <paramref name="headingDegrees"/> at
        /// each cell along the way (<see cref="Aetherium.Topology.IGridTopology.ResolveRelative"/>
        /// — per-cell because non-uniform topologies change direction sets cell to cell).
        /// The actor's heading is not updated — rotation is a separate action. Every step
        /// is validated via <see cref="MovementBlocker"/>; a multi-cell request can succeed
        /// partially — check <see cref="MoveOutcome.StepsTaken"/> and
        /// <see cref="MoveOutcome.BlockedReason"/>.
        /// </summary>
        public MoveOutcome TryMoveSteps(Character character, int headingDegrees, Aetherium.Model.RelativeDirection move, int distance)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                return MoveOutcome.Blocked(null, "Character has no location");

            if (distance < 1)
                return MoveOutcome.Blocked(location, "Distance must be at least 1");

            var current = location;
            var steps = 0;
            string? blocked = null;

            for (int i = 0; i < distance; i++)
            {
                var resolution = Topology.ResolveRelative(
                    Aetherium.Topology.GridCoord.From(current), headingDegrees, move);
                if (!resolution.Success)
                {
                    blocked = resolution.FailReason ?? "No path in that direction";
                    break;
                }

                var next = resolution.Step.Target.ToWorldLocation();
                blocked = MovementBlocker(character, next);
                if (blocked != null)
                    break;

                MoveEntity(character.EntityId, next);
                current = next;
                steps++;
            }

            if (steps > 0)
                CharacterMoveTimestamp = Guid.NewGuid();

            return new MoveOutcome
            {
                Success = steps > 0,
                StepsTaken = steps,
                FinalLocation = current,
                BlockedReason = blocked,
            };
        }

        /// <summary>
        /// Moves <paramref name="character"/> one level at a time along Z.
        /// Each step requires standing on a stair cell — terrain named
        /// "Upstairs"/"Downstairs", or a <see cref="CanAscend"/>/<see cref="CanDescend"/>
        /// component on the current location — and the landing cell must pass
        /// <see cref="MovementBlocker"/> like any other move.
        /// </summary>
        public MoveOutcome TryChangeLevel(Character character, int deltaZ)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                return MoveOutcome.Blocked(null, "Character has no location");

            if (deltaZ == 0)
                return MoveOutcome.Blocked(location, "Already on that level");

            var stepZ = Math.Sign(deltaZ);
            var current = location;
            var steps = 0;
            string? blocked = null;

            for (int i = 0; i < Math.Abs(deltaZ); i++)
            {
                if (!AllowsVerticalTravel(current, stepZ))
                {
                    blocked = "No stairs here";
                    break;
                }

                var next = current.FromDelta(0, 0, stepZ);
                blocked = MovementBlocker(character, next);
                if (blocked != null)
                    break;

                MoveEntity(character.EntityId, next);
                current = next;
                steps++;
            }

            if (steps > 0)
                CharacterMoveTimestamp = Guid.NewGuid();

            return new MoveOutcome
            {
                Success = steps > 0,
                StepsTaken = steps,
                FinalLocation = current,
                BlockedReason = blocked,
            };
        }

        /// <summary>
        /// Whether the cell at <paramref name="from"/> grants vertical travel.
        /// Stair terrain grants travel in either direction — generated stair
        /// pairs are vertically aligned (see AdvancedDungeonGenerator.ConnectLevels),
        /// so the same column is a stair cell on both levels. The
        /// CanAscend/CanDescend components keep the legacy TryMove convention
        /// (+Z is "ascend") for worlds/tests that grant travel per-location.
        /// </summary>
        private bool AllowsVerticalTravel(WorldLocation from, int stepZ)
        {
            var terrainName = GetTerrainType(from)?.Name;
            if (terrainName is "Upstairs" or "Downstairs")
                return true;

            if (stepZ > 0 && from.Has<CanAscend>())
                return true;
            if (stepZ < 0 && from.Has<CanDescend>())
                return true;

            return false;
        }

        public IDictionary<string, int> GetTerrainDistribution(IEnumerable<WorldLocation> locations)
        {
            var results = new Dictionary<string, int>();

            foreach (var loc in locations)
            {
                var terrainName = EntitiesByLocation.ContainsKey(loc) 
                    ? GetTerrain(loc)?.Type.Name
                    : "None";

                if (terrainName == null)
                    throw new InvalidOperationException("Terrain name is missing");

                if (results.ContainsKey(terrainName))
                    results[terrainName]++;
                else
                    results.Add(terrainName, 1);
            }

            return results;
        }

        public bool PassableTerrain(WorldLocation location) =>
            EntitiesByLocation.ContainsKey(location) && PassableTerrain(GetTerrainType(location));

        public bool PassableTerrain(TerrainType? terrainType)
        {
            if (terrainType == null)
                return false;

            // Prefer the explicit flag when set so new terrain types don't have to live
            // in the legacy switch below.
            if (terrainType.IsPassable.HasValue)
                return terrainType.IsPassable.Value;

            // Legacy name-based fallback so existing builders keep working without edits.
            return terrainType.Name switch
            {
                "Indoors" or "Upstairs" or "Downstairs" or "Road" or "Plains" or "Forest" or "Cave" => true,
                _ => false,
            };
        }

        /// <summary>
        /// Number of altitude bands, counted upward from a terrain tile's own band, that the terrain obstructs
        /// movement. Passable terrain returns 0 (blocks nothing); impassable terrain returns its
        /// "ObstructionHeight" setting when present, otherwise <see cref="DefaultWallHeight"/>. At band 0 this
        /// reduces to the classic passable/impassable distinction, so grounded behavior is unchanged.
        /// </summary>
        public int TerrainObstructionHeight(TerrainType? terrainType)
        {
            if (terrainType == null)
                return DefaultWallHeight; // unknown / missing terrain is treated as solid

            if (terrainType.Settings != null
                && terrainType.Settings.TryGetValue("ObstructionHeight", out var raw)
                && int.TryParse(raw, out var explicitHeight))
                return Math.Max(0, explicitHeight);

            return PassableTerrain(terrainType) ? 0 : DefaultWallHeight;
        }

        /// <summary>
        /// Whether anything in column (x,y) obstructs movement at the given altitude band, resolving terrain
        /// obstruction (anchored at the terrain's band, spanning its height) and entity
        /// <see cref="ObstructsMovement"/> obstructions (spanning their Height). Uses keyed location lookups
        /// only — never a full-world entity scan.
        /// </summary>
        public bool ColumnObstructsMovement(int x, int y, int band)
        {
            for (int z = MinBand; z <= band; z++)
            {
                if (!EntitiesByLocation.TryGetValue(new WorldLocation(x, y, z), out var entitiesAtLoc))
                    continue;

                foreach (var entity in entitiesAtLoc.Values)
                {
                    if (entity is Terrain)
                    {
                        var height = TerrainObstructionHeight(GetTerrainType(new WorldLocation(x, y, z)));
                        if (band < z + height)
                            return true;
                    }
                    else if (entity.Has<ObstructsMovement>())
                    {
                        var om = entity.Get<ObstructsMovement>();
                        if (om.Obstruction > 0 && band < z + om.Height)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Aggregate sight opacity (0 = clear .. 1 = fully opaque) contributed by <see cref="ObstructsView"/>
        /// obstructions covering the given band in column (x,y). A glass skylight (Opacity 0) contributes
        /// nothing even when it blocks movement, so observers can see through it.
        /// </summary>
        public double ColumnViewOpacity(int x, int y, int band)
        {
            double maxOpacity = 0;

            for (int z = MinBand; z <= band; z++)
            {
                if (!EntitiesByLocation.TryGetValue(new WorldLocation(x, y, z), out var entitiesAtLoc))
                    continue;

                foreach (var entity in entitiesAtLoc.Values)
                {
                    if (entity.Has<ObstructsView>())
                    {
                        var ov = entity.Get<ObstructsView>();
                        if (band < z + ov.Height && ov.Opacity > maxOpacity)
                            maxOpacity = ov.Opacity;
                    }
                }
            }

            return Math.Min(1.0, maxOpacity);
        }

        /// <summary>
        /// Band-aware passability. Grounded entities (no <see cref="Flight"/> component) use the existing
        /// ground-band <see cref="PassableTerrain(WorldLocation)"/> rule unchanged. Airborne flyers may enter a
        /// cell whose target band is within their [MinBand, MaxBand] range and is not obstructed at that band,
        /// so they pass over ground-band obstruction they fly above.
        /// </summary>
        public bool IsPassable(WorldLocation cell, Entity? forEntity = null)
        {
            if (forEntity == null || !forEntity.Has<Flight>())
                return PassableTerrain(cell);

            var flight = forEntity.Get<Flight>();
            if (flight.State == FlightState.Airborne)
            {
                if (cell.Z < flight.MinBand || cell.Z > flight.MaxBand)
                    return false;

                return !ColumnObstructsMovement(cell.X, cell.Y, cell.Z);
            }

            // Landed / taking off / landing flyers obey normal ground passability.
            return PassableTerrain(cell);
        }

        /// <summary>Returns a non-negative random integer less than <paramref name="maxExclusive"/> from the world's RNG.</summary>
        public int NextRandom(int maxExclusive) => Random.Shared.Next(maxExclusive);

        /// <summary>Whether a flyer with CanLand may land on / take off from the given terrain (world default set).</summary>
        public bool CanLandOn(TerrainType? terrainType) =>
            terrainType != null && LandingTerrainNames.Contains(terrainType.Name);

        /// <summary>
        /// Whether the given flyer may land on the given terrain. Landability depends on the flyer: if its
        /// <see cref="Flight.LandableTerrain"/> set is non-empty it governs (a bird lands on forest/mountain/water,
        /// a wheeled plane only on road/plains); otherwise the world's <see cref="LandingTerrainNames"/> applies.
        /// </summary>
        public bool CanLandOn(Flight flight, TerrainType? terrainType)
        {
            if (terrainType == null)
                return false;
            if (flight?.LandableTerrain != null && flight.LandableTerrain.Count > 0)
                return flight.LandableTerrain.Contains(terrainType.Name);
            return LandingTerrainNames.Contains(terrainType.Name);
        }

        /// <summary>
        /// The surface a flyer descending from <paramref name="fromBand"/> in column (x,y) comes to rest on: the
        /// highest of the terrain top and any structure top at or below that band. A terrain tile always presents
        /// a surface — the passable ground floor rests at the tile's own band, a tall terrain feature (wall,
        /// mountain) rests at its peak (tile band + height − 1). A structure (non-terrain obstruction) rests at
        /// its top band. Returns null when the column is open below the flyer (nothing to land on). This is pure
        /// geometry; whether the flyer <em>may</em> land on the returned surface is decided by the caller.
        /// </summary>
        public LandingSurface? SurfaceBelow(int x, int y, int fromBand)
        {
            int start = Math.Min(fromBand, MaxBand);

            int bestBand = int.MinValue;
            TerrainType? bestTerrain = null;

            // Terrain top: the column's terrain tile (typically the ground plane). Passable ground rests at its
            // own band; a tall terrain feature rests at its peak.
            for (int z = start; z >= MinBand; z--)
            {
                var terrain = GetTerrainType(new WorldLocation(x, y, z));
                if (terrain == null)
                    continue;

                int top = z + Math.Max(0, TerrainObstructionHeight(terrain) - 1);
                if (top <= fromBand)
                {
                    bestBand = top;
                    bestTerrain = terrain;
                }
                break; // the first terrain tile found scanning down is the column's floor
            }

            // Structure tops: non-terrain obstructions. The highest one at or below the flyer wins.
            for (int z = start; z >= MinBand; z--)
            {
                if (!EntitiesByLocation.TryGetValue(new WorldLocation(x, y, z), out var entitiesAtLoc))
                    continue;

                foreach (var entity in entitiesAtLoc.Values)
                {
                    if (entity is Terrain || !entity.Has<ObstructsMovement>())
                        continue;

                    var om = entity.Get<ObstructsMovement>();
                    if (om.Obstruction <= 0)
                        continue;

                    int top = z + Math.Max(0, om.Height - 1);
                    if (top <= fromBand && top > bestBand)
                    {
                        bestBand = top;
                        bestTerrain = null; // a structure top, not terrain
                    }
                }
            }

            if (bestBand == int.MinValue)
                return null;

            return new LandingSurface(new WorldLocation(x, y, bestBand), bestTerrain);
        }

        /// <summary>
        /// Emits a world event. This method allows external classes to emit events through the World instance.
        /// </summary>
        public void EmitEvent(WorldEvent worldEvent)
        {
            WorldEvents?.Invoke(worldEvent);
        }
    }
}

