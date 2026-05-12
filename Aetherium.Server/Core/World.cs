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

                    // Get-or-create the destination bucket so entities never leak into limbo
                    // when moving onto a cell that doesn't yet have an index entry (e.g. a
                    // newly-uncovered tile, or a cell whose only resident was just removed).
                    var entitiesAtDestination = EntitiesByLocation.GetOrAdd(
                        destination,
                        _ => new ConcurrentDictionary<string, Entity>());

                    if (entitiesAtDestination.TryAdd(Id, entity))
                    {
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
        }

        public bool TryMove(Character character, WorldDirection direction)
        {
            var location = character.Get<WorldLocation>();
            if (location == null)
                throw new InvalidOperationException("Character is missing WorldLocation");

            // Canonical engine convention: North = -Y, South = +Y, East = +X, West = -X.
            // This matches GameSession.MoveView and the rest of the perception stack.
            switch (direction)
            {
                case WorldDirection.North:
                    return TryMove(character, location.FromDelta(0, -1, 0));
                case WorldDirection.South:
                    return TryMove(character, location.FromDelta(0, +1, 0));
                case WorldDirection.West:
                    return TryMove(character, location.FromDelta(-1, 0, 0));
                case WorldDirection.East:
                    return TryMove(character, location.FromDelta(+1, 0, 0));
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
            if (!EntitiesByLocation.ContainsKey(location))
                return false;

            // stop players (including monsters) from existing in the same location.
            // Exclude the character itself so a no-op move doesn't trigger self-collision damage.
            var other = Entities.Values
                .OfType<Character>()
                .FirstOrDefault(p => p.EntityId != character.EntityId && p.Get<WorldLocation>() == location);
            if (other != null)
            {
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

            var up = currentLocation.FromDelta(0, 0, +1);
            var down = currentLocation.FromDelta(0, 0, -1);

            if (location == up && !currentLocation.Has<CanAscend>())
                return false;
            else if (location == down && !currentLocation.Has<CanDescend>())
                return false;

            if (PassableTerrain(location))
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
        /// Emits a world event. This method allows external classes to emit events through the World instance.
        /// </summary>
        public void EmitEvent(WorldEvent worldEvent)
        {
            WorldEvents?.Invoke(worldEvent);
        }
    }
}

