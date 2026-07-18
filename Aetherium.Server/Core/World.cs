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
        public Dictionary<string, TileType> TileTypes { get; protected set; }
        public Dictionary<string, TerrainType> TerrainTypes { get; protected set; }

        public List<WorldFeature> Features { get; set; }

        public ConcurrentDictionary<string, Entity> Entities { get; set; }
        public ConcurrentDictionary<WorldLocation, ConcurrentDictionary<string, Entity>> EntitiesByLocation { get; set; }
        public ConcurrentDictionary<string, Character> Characters { get; set; }

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

        Random rand = new Random();

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
            if (EntitiesByLocation.Count == 0)
                return null;

            while (true)
            {
                var es = EntitiesByLocation.ToList();
                var location = es[rand.Next(0, es.Count)].Key;

                if (PassableTerrain(location))
                    return location;
            }
        }

        public T SelectRandomEntity<T>(IList<string>? excludedEntityIds = null)
            where T : Entity
        {
            var exclusions = excludedEntityIds ?? new List<string>();

            var cs = Entities.OfType<T>()
                .Where(e => !exclusions.Contains(e.EntityId))
                .ToList();

            return cs[rand.Next(0, cs.Count)];
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

        int addEntityCalls = 0;
        public void AddEntity(Entity entity)
        {
            addEntityCalls++;

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
            var e = Entities[Id];
            if (e == null)
                throw new ArgumentException("EntityId not found");

            if (Entities.TryGetValue(Id, out var entity)
                && EntitiesByLocation.TryGetValue(entity.Get<WorldLocation>(), out var entitiesAtLocation)
                && entitiesAtLocation.TryRemove(Id, out var _)
                && Entities.TryRemove(Id, out var _))
            {
                if (e is Character)
                    Characters.TryRemove(e.EntityId, out var _);

                // If no more entities remain at this location, remove the location index entry
                if (entitiesAtLocation.IsEmpty)
                {
                    EntitiesByLocation.TryRemove(entity.Get<WorldLocation>(), out var _);
                }

                WorldEvents?.Invoke(new WorldEvent
                {
                    EventType = WorldEventType.EntityRemoved,
                    Location = entity.Get<WorldLocation>(),
                    Entity = entity
                });
            }
        }

        public void MoveEntity(string Id, WorldLocation destination)
        {
            if (Entities.TryGetValue(Id, out var entity))
            {
                if (entity.Get<WorldLocation>() == destination)
                    return;

                // remove from location index
                if (EntitiesByLocation.TryGetValue(entity.Get<WorldLocation>(), out var entitiesAtSource)
                    && entitiesAtSource.TryRemove(Id, out var _))
                {
                    // update location on entity
                    entity.Set(destination);

                    // Create the destination bucket if absent so entities (e.g. airborne flyers)
                    // can move into a previously-empty air cell, not just onto existing terrain.
                    var entitiesAtDestination = EntitiesByLocation.GetOrAdd(destination,
                        _ => new ConcurrentDictionary<string, Entity>());
                    if (entitiesAtDestination.TryAdd(Id, entity))
                    {
                        WorldEvents?.Invoke(new WorldEvent
                        {
                            EventType = WorldEventType.EntityMoved,
                            Location = entity.Get<WorldLocation>(),
                            Entity = entity
                        });
                    }
                }
            }
        }

        public bool TryMove(Character character, RelativeDirection direction)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                throw new InvalidOperationException("Character is missing WorldLocation");

            // TODO: transform RelativeDirection into WorldDirection
            // if player heading is North, and direction is Forward, go North
            // if player heading is East, and direction is Forward, go East
            // if player heading is South, and direction is Left, go East
            var destination = location.FromDelta(0, 0, 0);

            return TryMove(character, destination);
        }

        public bool TryMove(Character character, WorldDirection direction)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                throw new InvalidOperationException("Character is missing WorldLocation");

            switch (direction)
            {
                case WorldDirection.North:
                    return TryMove(character, location.FromDelta(0, -1, 0));
                case WorldDirection.South:
                    return TryMove(character, location.FromDelta(0, +1, 0));
                case WorldDirection.West:
                    return TryMove(character, location.FromDelta(0, -1, 0));
                case WorldDirection.East:
                    return TryMove(character, location.FromDelta(0, +1, 0));
                case WorldDirection.Up:
                    return TryMove(character, location.FromDelta(0, 0, +1));
                case WorldDirection.Down:
                    return TryMove(character, location.FromDelta(0, 0, -1));
                default:
                    return false;
            }
        }

        public bool TryMove(Character character, WorldLocation location)
        {
            var isAirborne = character.Has<Flight>()
                && character.Get<Flight>().State == FlightState.Airborne;

            // Grounded moves require a terrain-bearing cell; airborne flyers may enter open air.
            if (!isAirborne && !EntitiesByLocation.ContainsKey(location))
                return false;

            // stop players (including monsters) from existing in the same location.
            // Iterate the Characters index (not all Entities) so terrain entities don't inflate the scan.
            var other = Characters.Values
                .FirstOrDefault(p => p.Get<WorldLocation>() == location);
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

            switch (terrainType.Name)
            {
                case "Indoors":
                case "Upstairs":
                case "Downstairs":
                case "Road":
                case "Plains":
                case "Forest":
                case "Cave":
                    return true;
                default:
                    return false;
            }
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
        public int NextRandom(int maxExclusive) => rand.Next(maxExclusive);

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

