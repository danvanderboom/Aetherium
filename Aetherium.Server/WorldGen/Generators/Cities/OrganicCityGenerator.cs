using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen.Algorithms.Graphs;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators.Cities
{
    /// <summary>
    /// Generates organic cities using Voronoi diagrams and minimum spanning trees.
    /// Creates irregular, natural-looking city layouts.
    /// </summary>
    public class OrganicCityGenerator : IMapGenerator
    {
        // Extra-edge ratio for road MST: fraction of additional non-tree connections that
        // produce loops in the road network.
        private const double MstExtraEdgeRatio = 0.2;

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
            var rng = context.GetRandom("city:organic");
            var mst = MinimumSpanningTree.ComputeMSTWithExtraEdges(districts, MstExtraEdgeRatio, rng);

            // Carve roads along MST edges
            int roadWidth = GetParameter(context, "roadWidth", 2);
            foreach (var edge in mst)
            {
                CarveRoad(world, edge, roadWidth, context.ZLevel, context.Width, context.Height);
            }

            // Fill districts with buildings
            foreach (var district in districts)
            {
                FillDistrictWithBuildings(world, district, context, rng);
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
            var rng = context.GetRandom("city:organic");

            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(margin, context.Width - margin);
                int y = rng.Next(margin, context.Height - margin);

                centers.Add(new MinimumSpanningTree.Node(x, y, $"District{i}"));
            }

            return centers;
        }

        private void CarveRoad(World world, MinimumSpanningTree.Edge edge, int width, int z, int mapWidth, int mapHeight)
        {
            var path = MinimumSpanningTree.EdgesToPath(edge);

            foreach (var (x, y) in path)
            {
                for (int dy = -width / 2; dy <= width / 2; dy++)
                {
                    for (int dx = -width / 2; dx <= width / 2; dx++)
                    {
                        int wx = x + dx;
                        int wy = y + dy;
                        if (wx < 0 || wx >= mapWidth || wy < 0 || wy >= mapHeight)
                            continue;
                        world.SetTerrain("Road", new WorldLocation(wx, wy, z));
                    }
                }
            }
        }

        private void FillDistrictWithBuildings(World world, MinimumSpanningTree.Node district, GeneratorContext context, Random rng)
        {
            int radius = rng.Next(15, 25);
            int buildingCount = rng.Next(5, 12);

            for (int i = 0; i < buildingCount; i++)
            {
                double angle = rng.NextDouble() * 2 * Math.PI;
                double distance = rng.NextDouble() * radius;

                int bx = district.X + (int)(Math.Cos(angle) * distance);
                int by = district.Y + (int)(Math.Sin(angle) * distance);

                int size = rng.Next(4, 8);

                // Skip buildings that would extend outside the map.
                if (bx < 0 || by < 0 || bx + size > context.Width || by + size > context.Height)
                    continue;

                for (int dy = 0; dy < size; dy++)
                {
                    for (int dx = 0; dx < size; dx++)
                    {
                        var loc = new WorldLocation(bx + dx, by + dy, context.ZLevel);

                        // Don't clobber the road network with building tiles.
                        if (world.GetTerrainType(loc)?.Name == "Road")
                            continue;

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


