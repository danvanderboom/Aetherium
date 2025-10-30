using System;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen
{
    /// <summary>
    /// Context information for map generation, including random seed, dimensions, and generation parameters.
    /// </summary>
    public sealed class GeneratorContext
    {
        /// <summary>
        /// Random number generator seeded for deterministic generation.
        /// </summary>
        public Random Random { get; init; }

        /// <summary>
        /// Width of the map to generate.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Height of the map to generate.
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// Z-level (depth/height) for the map.
        /// </summary>
        public int ZLevel { get; init; } = 0;

        /// <summary>
        /// Optional seed for deterministic generation. If provided, Random is seeded with this value.
        /// </summary>
        public int? Seed { get; init; }

        /// <summary>
        /// Start location where the player should spawn.
        /// </summary>
        public WorldLocation? StartLocation { get; set; }

        /// <summary>
        /// Registry of available generation features.
        /// </summary>
        public MapGeneratorRegistry? FeatureRegistry { get; init; }

        /// <summary>
        /// Optional narrative ID for narrative-aware generation.
        /// </summary>
        public string? NarrativeId { get; set; }

        /// <summary>
        /// Generator-specific parameters.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, string>? GeneratorParams { get; set; }

        public GeneratorContext(int width, int height, int? seed = null)
        {
            Width = width;
            Height = height;
            Seed = seed;
            Random = seed.HasValue ? new Random(seed.Value) : new Random();
        }
    }
}
