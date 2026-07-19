using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen
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
        /// The grid tiling this world is generated for (docs/h3-topology.md, docs/grid-topologies.md).
        /// Threaded from the world's topology so a generator or pass can branch on the tiling —
        /// most importantly, an H3 (sphere) world enumerates its cells by resolution rather than
        /// scanning a Width×Height rectangle, since its <see cref="Aetherium.Components.WorldLocation"/>
        /// X/Y are two halves of a packed 64-bit cell index, not column/row. Defaults to square
        /// (the legacy behaviour); planar generators can keep ignoring it.
        /// </summary>
        public Aetherium.Topology.IGridTopology Topology { get; init; } = Aetherium.Topology.SquareTopology.Instance;

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

        /// <summary>Optional per-world economy recipe threaded from the bundle (via
        /// <see cref="WorldGenerationRequest.Economy"/>). The settlement seeder uses it when present;
        /// null → the engine default. Keeps goods/recipes data, not hard-coded.</summary>
        public Aetherium.Model.Economy.EconomyConfig? Economy { get; set; }

        /// <summary>
        /// Number of vertical levels to build (Z depth).
        /// </summary>
        public int Levels { get; set; } = 1;

        /// <summary>
        /// Semantic version for the generator logic. Changing this value with the same seed produces new output.
        /// <para>
        /// This property is <c>init</c>-only: setting it after construction via an object-initializer
        /// is fine, but mutating it mid-run would silently change the derived seed for any scope
        /// not yet created, leading to hidden non-determinism. Use object-initializer syntax:
        /// <c>new GeneratorContext(w, h, seed) { GeneratorVersion = "2.0.0" }</c>.
        /// </para>
        /// </summary>
        public string GeneratorVersion { get; init; } = "1.0.0";

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
            for (int i = 0; i < RngWarmupDraws; i++)
                rng.Next();
            _scopedRandoms[scope] = rng;
            return rng;
        }

        // Number of warm-up draws after constructing a scoped Random. Near-seed values
        // (scopes whose derived seeds differ by only a few bits) produce correlated initial
        // outputs from .NET's XOSHIRO256** algorithm; a brief warm-up decorrelates them.
        private const int RngWarmupDraws = 8;

        /// <summary>
        /// Returns true if a generator parameter with the given key is present.
        /// </summary>
        public bool HasParam(string key) =>
            GeneratorParams != null && GeneratorParams.ContainsKey(key);

        /// <summary>
        /// Reads an integer generator parameter, returning <paramref name="defaultValue"/> when the
        /// key is absent or unparseable, and clamping a parsed value into [<paramref name="min"/>,
        /// <paramref name="max"/>]. The default is returned as-is (not clamped) so callers can use an
        /// out-of-range sentinel to detect absence.
        /// </summary>
        public int GetIntParam(string key, int defaultValue, int min = int.MinValue, int max = int.MaxValue)
        {
            if (GeneratorParams != null && GeneratorParams.TryGetValue(key, out var raw)
                && int.TryParse(raw, out var value))
            {
                return Math.Clamp(value, min, max);
            }
            return defaultValue;
        }

        /// <summary>
        /// Reads a floating-point generator parameter, returning <paramref name="defaultValue"/> when
        /// the key is absent or unparseable, and clamping a parsed value into [<paramref name="min"/>,
        /// <paramref name="max"/>].
        /// </summary>
        public double GetDoubleParam(string key, double defaultValue, double min = double.MinValue, double max = double.MaxValue)
        {
            if (GeneratorParams != null && GeneratorParams.TryGetValue(key, out var raw)
                && double.TryParse(raw, out var value))
            {
                return Math.Clamp(value, min, max);
            }
            return defaultValue;
        }

        private static int DeriveSeed(int baseSeed, string version, string scope)
        {
            using var sha = SHA256.Create();
            var material = Encoding.UTF8.GetBytes($"{baseSeed}:{version}:{scope}");
            var hash = sha.ComputeHash(material);
            // BinaryPrimitives is endianness-explicit (always little-endian) so the derived seed
            // is identical on both little- and big-endian architectures.
            return BinaryPrimitives.ReadInt32LittleEndian(hash);
        }
    }
}

