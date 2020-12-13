using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using ConsoleGame;
using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public class World
    {
        public Dictionary<string, TileType> TileTypes { get; protected set; }

        public Dictionary<string, TerrainType> TerrainTypes { get; protected set; }

        public ConcurrentDictionary<string, Entity> Entities { get; set; }
        public ConcurrentDictionary<Location, ConcurrentDictionary<string, Entity>> EntitiesByLocation { get; set; }

        public event Action<WorldEvent> WorldEvents;

        Random rand = new Random();

        public World()
        {
            TerrainTypes = new Dictionary<string, TerrainType>();
            TileTypes = new Dictionary<string, TileType>();

            Entities = new ConcurrentDictionary<string, Entity>();
            EntitiesByLocation = new ConcurrentDictionary<Location, ConcurrentDictionary<string, Entity>>();
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

                if (EntitiesByLocation.TryGetValue(entity.Get<Location>(), out var existingDict))
                {
                    dict = existingDict;
                }
                else
                {
                    dict = new ConcurrentDictionary<string, Entity>();
                    EntitiesByLocation.TryAdd(entity.Get<Location>(), dict);
                }

                if (dict.TryAdd(entity.EntityId, entity))
                    WorldEvents?.Invoke(new WorldEvent
                    {
                        EventType = WorldEventType.EntityAdded,
                        Location = entity.Get<Location>(),
                        Entity = entity
                    });
            }
        }

        public void RemoveEntity(string Id)
        {
            if (Entities.TryGetValue(Id, out var entity)
                && EntitiesByLocation.TryGetValue(entity.Get<Location>(), out var entitiesAtLocation)
                && entitiesAtLocation.TryRemove(Id, out var _)
                && Entities.TryRemove(Id, out var _))
            {
                WorldEvents?.Invoke(new WorldEvent
                {
                    EventType = WorldEventType.EntityRemoved,
                    Location = entity.Get<Location>(),
                    Entity = entity
                });
            }
        }
    }
}
