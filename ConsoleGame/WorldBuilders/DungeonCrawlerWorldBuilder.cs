using System;
using System.Linq;
using System.Collections.Generic;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;

namespace ConsoleGame.WorldBuilders
{
    public class DungeonCrawlerWorldBuilder
    {
        World world;

        Random rand = new Random();

        public DungeonCrawlerWorldBuilder(World world)
        {
            this.world = world;
        }

        int ForceInRange(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

        int RandomSign() => rand.Next(0, 2) == 0 ? 1 : -1;

        public void Build()
        {
            // river
            var riverStartY = -50;
            var riverCenter = 0;
            var riverCenterMaxChange = 2;
            var riverCenterChangeTurns = 3;
            var riverMinWidth = 4;
            var riverMaxWidth = 12;
            var riverMaxWidthChange = 2;
            var riverLength = 100;
            var riverMinBorderWidth = 3;
            var riverMaxBorderWidth = 9;
            var riverBorderTerrainName = "Forest";

            var riverWidth = rand.Next(riverMinWidth, riverMaxWidth + 1);

            for (int line = 0; line < riverLength; line++)
            {
                riverWidth = ForceInRange(
                    riverWidth + (RandomSign() * rand.Next(0, riverMaxWidthChange + 1)), 
                    riverMinWidth, 
                    riverMaxWidth);

                if (line % riverCenterChangeTurns == 0)
                    riverCenter += RandomSign() * rand.Next(0, riverCenterMaxChange + 1);

                var x = riverCenter - ((riverWidth + 1) / 2);
                var y = riverStartY + line;

                // left border of river
                var leftBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                AddTerrain(riverBorderTerrainName, 
                    new Location(x - leftBorderWidth, y, 0), 
                    new Size3d(1, leftBorderWidth, 1));

                // river
                AddTerrain("Water",
                    new Location(x, y, 0),
                    new Size3d(1, riverWidth, 1));

                // right border of river
                var rightBorderWidth = rand.Next(riverMinBorderWidth, riverMaxBorderWidth + 1);
                AddTerrain(riverBorderTerrainName, 
                    new Location(x + riverWidth, y, 0), 
                    new Size3d(1, rightBorderWidth, 1));

                //var expectedLocationCount = leftBorderWidth + riverWidth + rightBorderWidth;
                //var test = GetTerrain(new Location(x - leftBorderWidth, y, 0), new Size3d(1, expectedLocationCount, 1));
                //if (test.Count != expectedLocationCount)
                //    break;
            }
        }

        List<Terrain> GetTerrain(Location location, Size3d size)
        {
            var results = new List<Terrain>();

            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                        foreach (var entity in world.EntitiesByLocation[location.FromDelta(x, y, z)].Values)
                            if (entity is Terrain)
                                results.Add(entity as Terrain);

            return results;
        }

        void AddTerrain(string name, Location location, Size3d size)
        {
            for (int z = 0; z < size.Depth; z++)
                for (int y = 0; y < size.Length; y++)
                    for (int x = 0; x < size.Width; x++)
                            AddTerrain(name, location.FromDelta(x, y, z));
        }

        Terrain AddTerrain(string name, Location location)
        {
            switch (name)
            {
                case "Indoors":
                    var indoorsType = world.TerrainTypes["Indoors"];
                    var indoors = new Terrain();
                    indoors.Set(new Tile { Type = indoorsType.TileType });
                    indoors.Set(location);
                    world.AddEntity(indoors);
                    return indoors;
                case "Water":
                    var waterType = world.TerrainTypes["Water"];
                    var water = new Terrain();
                    water.Set(new Tile { Type = waterType.TileType });
                    water.Set(location);
                    world.AddEntity(water);
                    return water;
                case "Plains":
                    var plainsType = world.TerrainTypes["Plains"];
                    var plains = new Terrain();
                    plains.Set(new Tile { Type = plainsType.TileType });
                    plains.Set(location);
                    world.AddEntity(plains);
                    return plains;
                case "Forest":
                    var forestType = world.TerrainTypes["Forest"];
                    var forest = new Terrain();
                    forest.Set(new Tile { Type = forestType.TileType });
                    forest.Set(location);
                    world.AddEntity(forest);
                    return forest;
                default:
                    return null;
                    break;
            }
        }
    }
}
