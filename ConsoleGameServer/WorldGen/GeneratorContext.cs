using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen
{
    /// <summary>
    /// Context information for map generation, including random seed, dimensions, and generation parameters.
    /// </summary>
    public sealed class GeneratorContext
    {
        private const string GlobalNamespace = "global";
        private readonly Dictionary<string, Random> _scopedRandoms = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Random number generator seeded for deterministic generation.
        /// Namespaces can be retrieved via <see cref="GetRandom"/>.
        /// </summary>
        public Random Random => GetRandom(GlobalNamespace);

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
        public int? Seed { get; }

        /// <summary>
        /// Seed value used internally after applying fallbacks when <see cref="Seed"/> is null.
        /// </summary>
        public int EffectiveSeed { get; }

        /// <summary>
        /// Start location where the player should spawn.
        /// </summary>
        public WorldLocation? StartLocation { get; set; }

        /// <summary>
        /// Location of the primary objective (boss, exit, quest target) when applicable.
        /// </summary>
        public WorldLocation? ObjectiveLocation { get; set; }

        /// <summary>
        /// Represents the primary critical path (e.g., start → objective) recorded during generation.
        /// </summary>
        public List<WorldLocation> PrimaryPath { get; } = new List<WorldLocation>();

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

        /// <summary>
        /// Number of vertical levels to build (Z depth).
        /// </summary>
        public int Levels { get; set; } = 1;

        /// <summary>
        /// Semantic version for the generator logic. Changing this value with the same seed produces new output.
        /// </summary>
        public string GeneratorVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Metrics collected during generation.
        /// </summary>
        public GenerationMetrics Metrics { get; } = new GenerationMetrics();

        /// <summary>
        /// Arbitrary data bag for passes to exchange intermediate artifacts.
        /// </summary>
        public Dictionary<string, object> PhaseArtifacts { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Narrative constraints applied to generation.
        /// </summary>
        public NarrativeGenerationConstraints NarrativeConstraints { get; set; } = new NarrativeGenerationConstraints();

        public GeneratorContext(int width, int height, int? seed = null)
        {
            Width = width;
            Height = height;
            Seed = seed;
            EffectiveSeed = seed ?? RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// Retrieves a deterministic RNG scoped to the provided namespace. Each namespace yields
        /// a distinct random stream derived from the effective seed and generator version.
        /// </summary>
        public Random GetRandom(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                scope = GlobalNamespace;
            }

            if (_scopedRandoms.TryGetValue(scope, out var rng))
            {
                return rng;
            }

            var seed = DeriveSeed(EffectiveSeed, GeneratorVersion, scope);
            rng = new Random(seed);
            _scopedRandoms[scope] = rng;
            return rng;
        }

        private static int DeriveSeed(int baseSeed, string version, string scope)
        {
            using var sha = SHA256.Create();
            var material = Encoding.UTF8.GetBytes($"{baseSeed}:{version}:{scope}");
            var hash = sha.ComputeHash(material);
            return BitConverter.ToInt32(hash, 0);
        }
    }
}
