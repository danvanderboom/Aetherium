using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.Components;

namespace ConsoleGame.WorldBuilders
{
    public abstract class WorldBuilder
    {
        World world;

        public WorldBuilder(World world)
        {
            this.world = world;
        }

        public abstract World Build(WorldBuilderOptions options = null);

        public abstract World Expand(WorldBuilderOptions options = null);

        protected List<Terrain> GetTerrain(WorldLocation location, Size3d size)
        {
            var results = new List<Terrain>();

            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        foreach (var terrainEntity in world.EntitiesByLocation[location.FromDelta(x, y, z)].Values.OfType<Terrain>())
                            results.Add(terrainEntity);

            return results;
        }

        protected void AddTerrain(string name, WorldLocation location, Size3d size)
        {
            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        AddTerrain(name, location.FromDelta(x, y, z));
        }

        protected Terrain? AddTerrain(string name, WorldLocation location)
        {
            var terrainType = world.TerrainTypes[name];
            if (terrainType == null)
                throw new InvalidOperationException($"Terrain not registered: '{name}'");

            var terrain = new Terrain();
            terrain.Set(new Tile { Type = terrainType?.TileType ?? TileType.None });
            terrain.Set(location);

            world.AddEntity(terrain);

            return terrain;
        }
    }
}
