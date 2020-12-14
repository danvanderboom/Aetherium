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
        public ConcurrentDictionary<WorldLocation, ConcurrentDictionary<string, Entity>> EntitiesByLocation { get; set; }

        public ConcurrentDictionary<string, ConcurrentDictionary<string, WorldFeature>> WorldFeaturesByName { get; set; }

        Random rand = new Random();

        public World()
        {
            TerrainTypes = new Dictionary<string, TerrainType>();
            TileTypes = new Dictionary<string, TileType>();

            Entities = new ConcurrentDictionary<string, Entity>();
            EntitiesByLocation = new ConcurrentDictionary<WorldLocation, ConcurrentDictionary<string, Entity>>();
            WorldFeaturesByName = new ConcurrentDictionary<string, ConcurrentDictionary<string, WorldFeature>>();
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
                    EntitiesByLocation.TryAdd(entity.Get<WorldLocation>(), dict);
                }

                dict.TryAdd(entity.EntityId, entity);

                //if (dict.TryAdd(entity.EntityId, entity))
                //    WorldEvents?.Invoke(new WorldEvent
                //    {
                //        EventType = WorldEventType.EntityAdded,
                //        Location = entity.Get<WorldLocation>(),
                //        Entity = entity
                //    });
            }
        }

        public void RemoveEntity(string Id)
        {
            if (Entities.TryGetValue(Id, out var entity)
                && EntitiesByLocation.TryGetValue(entity.Get<WorldLocation>(), out var entitiesAtLocation)
                && entitiesAtLocation.TryRemove(Id, out var _)
                && Entities.TryRemove(Id, out var _))
            {
                //WorldEvents?.Invoke(new WorldEvent
                //{
                //    EventType = WorldEventType.EntityRemoved,
                //    Location = entity.Get<WorldLocation>(),
                //    Entity = entity
                //});
            }
        }
    }
}
