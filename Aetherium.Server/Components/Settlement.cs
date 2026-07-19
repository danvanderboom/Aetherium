using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// The four settlement tiers, smallest to largest. Tier drives everything downstream: how far
    /// apart the generator spaces sites, how wide the built-up core is, how big the population is,
    /// and (in the economy layer) how much a settlement produces and consumes. A planet is mostly
    /// villages with a handful of capitals, like the real one.
    /// </summary>
    public enum SettlementTier
    {
        Village = 0,
        Town = 1,
        City = 2,
        Capital = 3,
    }

    /// <summary>
    /// Marks an entity as a settlement — a persistent, queryable place on the map that the economy
    /// layer attaches producers/consumers to and that transport networks connect. Unlike the planar
    /// generators' throwaway local record, this is a real ECS component so a settlement survives on
    /// the world and can be found by systems (<c>world.Characters</c>-style scans filter on
    /// <c>Has&lt;Settlement&gt;()</c>).
    ///
    /// <para>The <see cref="Biome"/> is the terrain the site was founded on <em>before</em> the core
    /// was stamped — it decides what the place naturally produces (forest → timber, hills → ore,
    /// plains → grain, and a <see cref="Coastal"/> site adds fish/port trade). Population and core
    /// radius scale with <see cref="Tier"/>.</para>
    /// </summary>
    public class Settlement : Component
    {
        public string Name { get; set; } = string.Empty;

        public SettlementTier Tier { get; set; } = SettlementTier.Village;

        /// <summary>Nominal population — flavour, and a scale factor for production/consumption.</summary>
        public int Population { get; set; }

        /// <summary>Terrain the site was founded on, before the core overwrote it. Drives production.</summary>
        public string Biome { get; set; } = "Plains";

        /// <summary>Radius (in cells) of the built-up core stamped at the site.</summary>
        public int CoreRadius { get; set; }

        /// <summary>True when the site borders water (sea or river) — enables fishing and port trade.</summary>
        public bool Coastal { get; set; }

        public Settlement() : base() { }
    }
}
