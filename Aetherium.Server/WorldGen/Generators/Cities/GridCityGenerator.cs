using System;
using System.Collections.Generic;
using System.Drawing;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen.Algorithms.Sampling;
using Aetherium.WorldGen.Prefabs;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators.Cities
{
    /// <summary>
    /// Generates Manhattan-style grid cities with streets and buildings.
    /// </summary>
    public class GridCityGenerator : IMapGenerator
    {
        private readonly WorldBuilder _baseBuilder;

        public GridCityGenerator()
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

            // Get generation parameters
            int blockSize = GetParameter(context, "blockSize", 12);
            int streetWidth = GetParameter(context, "streetWidth", 2);
            
            // Fill with walls (boundaries)
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, context.ZLevel));
                }
            }

            // Horizontal streets
            for (int y = streetWidth; y < context.Height - streetWidth; y += blockSize + streetWidth)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    for (int w = 0; w < streetWidth; w++)
                    {
                        if (y + w < context.Height)
                        {
                            world.SetTerrain("Road", new WorldLocation(x, y + w, context.ZLevel));
                        }
                    }
                }
            }

            // Vertical streets
            for (int x = streetWidth; x < context.Width - streetWidth; x += blockSize + streetWidth)
            {
                for (int y = 0; y < context.Height; y++)
                {
                    for (int w = 0; w < streetWidth; w++)
                    {
                        if (x + w < context.Width)
                        {
                            world.SetTerrain("Road", new WorldLocation(x + w, y, context.ZLevel));
                        }
                    }
                }
            }

            // Identify blocks between streets
            var blocks = IdentifyBlocks(context.Width, context.Height, blockSize, streetWidth);

            // Place buildings in blocks using Poisson sampling
            foreach (var block in blocks)
            {
                PlaceBuildingsInBlock(world, block, context);
            }

            // Set start location at a street intersection
            context.StartLocation = new WorldLocation(streetWidth, streetWidth, context.ZLevel);

            return world;
        }

        private List<Rectangle> IdentifyBlocks(int width, int height, int blockSize, int streetWidth)
        {
            var blocks = new List<Rectangle>();

            for (int y = 0; y < height; y += blockSize + streetWidth)
            {
                for (int x = 0; x < width; x += blockSize + streetWidth)
                {
                    int blockX = x + streetWidth;
                    int blockY = y + streetWidth;
                    int blockW = Math.Min(blockSize, width - blockX);
                    int blockH = Math.Min(blockSize, height - blockY);

                    if (blockW > 2 && blockH > 2)
                    {
                        blocks.Add(new Rectangle(blockX, blockY, blockW, blockH));
                    }
                }
            }

            return blocks;
        }

        private const int MaxBuildingSize = 6; // exclusive upper bound for rng.Next
        private const int MinBuildingSize = 3;

        private void PlaceBuildingsInBlock(World world, Rectangle block, GeneratorContext context)
        {
            // Poisson minimum distance must exceed the largest building footprint + 1 cell of
            // wall buffer; otherwise adjacent samples can yield overlapping buildings even
            // though they satisfy the Poisson constraint.
            var rng = context.GetRandom("city:grid");
            var minDistance = Math.Max(MaxBuildingSize, 6.0);
            var sampler = new PoissonDiscSampling(block.Width, block.Height, minDistance, rng);
            var positions = sampler.Generate();

            foreach (var (relX, relY) in positions)
            {
                int absX = block.X + (int)relX;
                int absY = block.Y + (int)relY;

                int buildingSize = rng.Next(MinBuildingSize, MaxBuildingSize);
                
                for (int by = 0; by < buildingSize && absY + by < block.Y + block.Height; by++)
                {
                    for (int bx = 0; bx < buildingSize && absX + bx < block.X + block.Width; bx++)
                    {
                        if (bx == 0 || bx == buildingSize - 1 || by == 0 || by == buildingSize - 1)
                        {
                            // Walls
                            world.SetTerrain("Wall", new WorldLocation(absX + bx, absY + by, context.ZLevel));
                        }
                        else
                        {
                            // Interior
                            world.SetTerrain("Indoors", new WorldLocation(absX + bx, absY + by, context.ZLevel));
                        }
                    }
                }

                // Add door on side facing nearest street
                int doorX = absX + buildingSize / 2;
                int doorY = absY;
                if (absX - block.X < block.Width / 2)
                {
                    doorX = absX; // West side
                    doorY = absY + buildingSize / 2;
                }
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


