using System.Collections.Generic;

namespace Aetherium.Server.Economy
{
    /// <summary>
    /// The goods vocabulary and the biome→production recipes for the first economy slice. Four raw goods,
    /// each with real producers and real consumers so trade actually flows: grain (the plains, and thinly
    /// the desert), timber (forests), ore (hills), and fish (any coastal town). <em>Every</em> settlement
    /// consumes all four — everyone eats and builds — so a place is a net exporter of what its hinterland
    /// yields and an importer of the rest, and the road network carries the difference.
    ///
    /// <para>These recipes are hard-coded here for the slice; the engine's data-driven principle says they
    /// should become per-world data (a goods/recipe table on the bundle) once the shape settles, exactly as
    /// death and abilities did. Kept in one place so that move is a lift-and-shift.</para>
    /// </summary>
    public static class Goods
    {
        public const string Grain = "Grain";
        public const string Timber = "Timber";
        public const string Ore = "Ore";
        public const string Fish = "Fish";

        public static readonly string[] All = { Grain, Timber, Ore, Fish };

        /// <summary>Equilibrium price of each good — what it costs when stock sits exactly at target.</summary>
        public static readonly IReadOnlyDictionary<string, double> BasePrice = new Dictionary<string, double>
        {
            [Grain] = 4.0,
            [Timber] = 6.0,
            [Ore] = 10.0,
            [Fish] = 5.0,
        };

        /// <summary>Per-head, per-step consumption every settlement incurs — the universal basket.</summary>
        public static readonly IReadOnlyDictionary<string, double> ConsumePerPop = new Dictionary<string, double>
        {
            [Grain] = 0.0040,
            [Timber] = 0.0030,
            [Ore] = 0.0020,
            [Fish] = 0.0020,
        };

        /// <summary>Extra fish a coastal settlement lands per head, per step.</summary>
        public const double CoastalFishPerPop = 0.0060;

        /// <summary>The good a biome's hinterland yields, and how much per head per step. Null → the biome
        /// produces no primary good (a pure trade/consumer town).</summary>
        public static (string Good, double PerPop)? PrimaryProduction(string biome) => biome switch
        {
            "Plains" => (Grain, 0.0100),
            "Forest" => (Timber, 0.0100),
            "Hills" => (Ore, 0.0100),
            "Desert" => (Grain, 0.0050),   // oasis agriculture — thinner than the plains
            _ => null,
        };
    }
}
