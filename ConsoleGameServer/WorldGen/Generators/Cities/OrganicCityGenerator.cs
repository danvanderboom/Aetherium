using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.WorldGen.Algorithms.Graphs;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame.WorldGen.Generators.Cities
{
    /// <summary>
    /// Generates organic cities using Voronoi diagrams and minimum spanning trees.
    /// Creates irregular, natural-looking city layouts.
    /// </summary>
    public class OrganicCityGenerator : IMapGenerator
    {
        private readonly WorldBuilder _baseBuilder;

        public OrganicCityGenerator()
        {
            _baseBuilder = new TestMazeWorldBuilder();
        }

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            
            // Setup tile and terrain types
            if (_baseBuilder is TestMazeWorldBuilder testBuilder)
            {
                var tileTypes = testBuilder.TileTypes;
                world.AddTileTypes(tileTypes);
                world.AddTerrainTypes(testBuilder.CreateTerrainTypes(tileTypes));
            }

            // Fill with plains (outdoor)
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Plains", new WorldLocation(x, y, context.ZLevel));
                }
            }

            // Generate district centers
            int districtCount = GetParameter(context, "districts", 5);
            var districts = GenerateDistrictCenters(context, districtCount);

            // Build road network using MST
            var mst = MinimumSpanningTree.ComputeMSTWithExtraEdges(districts, 0.2, context.Random);

            // Carve roads along MST edges
            int roadWidth = GetParameter(context, "roadWidth", 2);
            foreach (var edge in mst)
            {
                CarveRoad(world, edge, roadWidth, context.ZLevel);
            }

            // Fill districts with buildings
            foreach (var district in districts)
            {
                FillDistrictWithBuildings(world, district, context);
            }

            // Set start location at first district center
            if (districts.Count > 0)
            {
                var firstDistrict = districts[0];
                context.StartLocation = new WorldLocation(firstDistrict.X, firstDistrict.Y, context.ZLevel);
            }

            return world;
        }

        private List<MinimumSpanningTree.Node> GenerateDistrictCenters(GeneratorContext context, int count)
        {
            var centers = new List<MinimumSpanningTree.Node>();
            int margin = 10;

            for (int i = 0; i < count; i++)
            {
                int x = context.Random.Next(margin, context.Width - margin);
                int y = context.Random.Next(margin, context.Height - margin);
                
                centers.Add(new MinimumSpanningTree.Node(x, y, $"District{i}"));
            }

            return centers;
        }

        private void CarveRoad(World world, MinimumSpanningTree.Edge edge, int width, int z)
        {
            var path = MinimumSpanningTree.EdgesToPath(edge);

            foreach (var (x, y) in path)
            {
                // Widen the road
                for (int dy = -width / 2; dy <= width / 2; dy++)
                {
                    for (int dx = -width / 2; dx <= width / 2; dx++)
                    {
                        var loc = new WorldLocation(x + dx, y + dy, z);
                        world.SetTerrain("Road", loc);
                    }
                }
            }
        }

        private void FillDistrictWithBuildings(World world, MinimumSpanningTree.Node district, GeneratorContext context)
        {
            int radius = context.Random.Next(15, 25);
            int buildingCount = context.Random.Next(5, 12);

            // Place buildings in a radial pattern around district center
            for (int i = 0; i < buildingCount; i++)
            {
                double angle = context.Random.NextDouble() * 2 * Math.PI;
                double distance = context.Random.NextDouble() * radius;
                
                int bx = district.X + (int)(Math.Cos(angle) * distance);
                int by = district.Y + (int)(Math.Sin(angle) * distance);

                // Create building
                int size = context.Random.Next(4, 8);
                for (int dy = 0; dy < size; dy++)
                {
                    for (int dx = 0; dx < size; dx++)
                    {
                        var loc = new WorldLocation(bx + dx, by + dy, context.ZLevel);
                        
                        if (dx == 0 || dx == size - 1 || dy == 0 || dy == size - 1)
                        {
                            world.SetTerrain("Wall", loc);
                        }
                        else
                        {
                            world.SetTerrain("Indoors", loc);
                        }
                    }
                }

                // Add door
                int doorX = bx + size / 2;
                int doorY = by;
                world.SetTerrain("Indoors", new WorldLocation(doorX, doorY, context.ZLevel));
            }
        }

        private int GetParameter(GeneratorContext context, string key, int defaultValue)
        {
            if (context.GeneratorParams != null &&
                context.GeneratorParams.TryGetValue(key, out var value) &&
                int.TryParse(value, out var intValue))
            {
                return intValue;
            }
            return defaultValue;
        }
    }
}

