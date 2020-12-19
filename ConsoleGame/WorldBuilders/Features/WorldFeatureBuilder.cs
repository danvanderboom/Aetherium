using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.Components;

namespace ConsoleGame.WorldBuilders
{
    public abstract class WorldFeatureBuilder
    {
        protected World World { get; set; }

        protected WorldFeature Feature { get; set; }

        public WorldFeatureBuilder(World world, WorldFeature feature)
        {
            World = world;
            Feature = feature;
        }

        public abstract void Build(); // WorldBuilderOptions options = null);

        protected void GetTerrain(WorldLocation location) =>
            World.EntitiesByLocation[location].Values
            .OfType<Terrain>()
            .FirstOrDefault();

        protected List<Terrain> GetTerrain(WorldChunk chunk) =>
            GetTerrain(chunk.Location, chunk.Size);

        protected List<Terrain> GetTerrain(WorldLocation location, Size3d size)
        {
            var results = new List<Terrain>();

            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        foreach (var terrainEntity in World.EntitiesByLocation[location.FromDelta(x, y, z)].Values.OfType<Terrain>())
                            results.Add(terrainEntity);

            return results;
        }

        protected void SetTerrain(string name, WorldChunk chunk) =>
            SetTerrain(name, chunk.Location, chunk.Size);

        protected void SetTerrain(string name, WorldLocation location, Size3d size)
        {
            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        SetTerrain(name, location.FromDelta(x, y, z));
        }

        protected Terrain? SetTerrain(string name, WorldLocation location)
        {
            var terrainType = World.TerrainTypes[name];
            if (terrainType == null)
                throw new InvalidOperationException($"Terrain not registered: '{name}'");

            var terrain = new Terrain();
            terrain.Set(new Tile { Type = terrainType?.TileType ?? TileType.None });
            terrain.Set(location);

            World.AddEntity(terrain);

            return terrain;
        }

        Random rand = new Random();

        // TODO: move to dedicated Randomizer class?
        protected int RandomSign() => rand.Next(0, 2) == 0 ? 1 : -1;
    }
}
