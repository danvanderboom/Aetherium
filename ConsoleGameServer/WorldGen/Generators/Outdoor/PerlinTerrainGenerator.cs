using System;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.WorldGen.Algorithms.Noise;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Generates outdoor terrain using Perlin noise for natural-looking landscapes.
    /// Uses threshold-based terrain assignment for different biomes.
    /// </summary>
    public class PerlinTerrainGenerator : IMapGenerator
    {
        private readonly WorldBuilder _baseBuilder;

        public PerlinTerrainGenerator()
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
            double scale = GetParameter(context, "scale", 0.05);
            int octaves = GetParameter(context, "octaves", 4);
            double persistence = GetParameter(context, "persistence", 0.5);
            double lacunarity = GetParameter(context, "lacunarity", 2.0);
            
            // Thresholds for terrain types
            double waterThreshold = GetParameter(context, "waterThreshold", 0.3);
            double plainsThreshold = GetParameter(context, "plainsThreshold", 0.5);
            double forestThreshold = GetParameter(context, "forestThreshold", 0.7);
            double mountainThreshold = GetParameter(context, "mountainThreshold", 0.85);

            // Create noise generator
            var noise = new PerlinNoise(context.EffectiveSeed);

            // Generate terrain
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    // Sample noise at scaled coordinates
                    double noiseValue = noise.FractalNoiseNormalized(
                        x * scale,
                        y * scale,
                        octaves,
                        persistence,
                        lacunarity);

                    // Determine terrain type based on noise value
                    string terrainType = DetermineTerrainType(
                        noiseValue,
                        waterThreshold,
                        plainsThreshold,
                        forestThreshold,
                        mountainThreshold);

                    var location = new WorldLocation(x, y, context.ZLevel);
                    world.SetTerrain(terrainType, location);
                }
            }

            // Find a suitable start location (on Plains)
            context.StartLocation = FindStartLocation(world, context);

            return world;
        }

        private string DetermineTerrainType(
            double noiseValue,
            double waterThreshold,
            double plainsThreshold,
            double forestThreshold,
            double mountainThreshold)
        {
            if (noiseValue < waterThreshold)
                return "Water";
            else if (noiseValue < plainsThreshold)
                return "Plains";
            else if (noiseValue < forestThreshold)
                return "Forest";
            else if (noiseValue < mountainThreshold)
                return "Plains"; // Hills
            else
                return "Mountain";
        }

        private WorldLocation FindStartLocation(World world, GeneratorContext context)
        {
            // Try to find a Plains location near the center
            int centerX = context.Width / 2;
            int centerY = context.Height / 2;
            int searchRadius = Math.Min(context.Width, context.Height) / 4;

            for (int radius = 0; radius < searchRadius; radius++)
            {
                for (int angle = 0; angle < 360; angle += 10)
                {
                    double rad = angle * Math.PI / 180.0;
                    int x = centerX + (int)(radius * Math.Cos(rad));
                    int y = centerY + (int)(radius * Math.Sin(rad));

                    if (x >= 0 && x < context.Width && y >= 0 && y < context.Height)
                    {
                        var loc = new WorldLocation(x, y, context.ZLevel);
                        if (world.EntitiesByLocation.ContainsKey(loc) && world.PassableTerrain(loc))
                        {
                            return loc;
                        }
                    }
                }
            }

            // Fallback to center
            return new WorldLocation(centerX, centerY, context.ZLevel);
        }

        private double GetParameter(GeneratorContext context, string key, double defaultValue)
        {
            if (context.GeneratorParams != null &&
                context.GeneratorParams.TryGetValue(key, out var value) &&
                double.TryParse(value, out var doubleValue))
            {
                return doubleValue;
            }
            return defaultValue;
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

