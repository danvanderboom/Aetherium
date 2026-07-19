using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Economy
{
    /// <summary>
    /// Per-world economy recipe (engine data-driven principle): the goods vocabulary, the universal
    /// consumption basket, the coastal bonus, and the biome→production map that seed every settlement's
    /// producers/consumers/markets. Threaded from a game bundle exactly like death and ability config;
    /// when a bundle omits it the engine falls back to the built-in default, so worlds that never set
    /// one behave byte-for-byte as before. See <c>Aetherium.Server.Economy.Goods.DefaultConfig()</c> for
    /// the default and <c>EconomySeeder.Seed</c> for how a settlement consumes this.
    /// </summary>
    [GenerateSerializer]
    public class EconomyConfig
    {
        /// <summary>The full goods vocabulary. Each carries its equilibrium price and the per-head,
        /// per-step amount every settlement consumes of it (the universal basket — everyone eats and
        /// builds, so trade always has demand to flow against).</summary>
        [Id(0)] public List<GoodDef> Goods { get; set; } = new();

        /// <summary>The good a coastal settlement additionally lands (e.g. fish). Null → no coastal bonus.</summary>
        [Id(1)] public string? CoastalGood { get; set; }

        /// <summary>Extra <see cref="CoastalGood"/> a coastal settlement produces per head, per step.</summary>
        [Id(2)] public double CoastalPerPop { get; set; }

        /// <summary>What each biome's hinterland yields, and how much per head per step. A biome absent
        /// here produces no primary good (a pure trade/consumer town).</summary>
        [Id(3)] public List<BiomeProduction> Production { get; set; } = new();
    }

    /// <summary>A tradeable good: its name, equilibrium price, and universal per-head consumption rate.</summary>
    [GenerateSerializer]
    public class GoodDef
    {
        [Id(0)] public string Name { get; set; } = string.Empty;
        [Id(1)] public double BasePrice { get; set; } = 1.0;
        [Id(2)] public double ConsumePerPop { get; set; }
    }

    /// <summary>A biome's primary output: the good its hinterland yields and the per-head, per-step rate.</summary>
    [GenerateSerializer]
    public class BiomeProduction
    {
        [Id(0)] public string Biome { get; set; } = string.Empty;
        [Id(1)] public string Good { get; set; } = string.Empty;
        [Id(2)] public double PerPop { get; set; }
    }
}
