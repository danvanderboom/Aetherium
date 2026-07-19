using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Per-character overrides of the world's memory dynamics (add-memory-dynamics) — how good this
    /// individual's memory is, as data rather than an engine constant. Absent component ⇒ world
    /// defaults. A forgetful creature might set <see cref="HalfLifeMultiplier"/> = 0.2; an eidetic one
    /// 5.0. Only consulted when the world's <c>MemoryPolicy.DynamicsEnabled</c> is true.
    /// </summary>
    public class MemoryProfile : Component
    {
        /// <summary>
        /// Scales the effective base decay half-life. &lt;1 forgets faster, &gt;1 slower. Default 1.
        /// </summary>
        public double HalfLifeMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Scales the world's stability growth factor on each spaced reinforcement. &gt;1 makes this
        /// character's memories entrench faster. Default 1.
        /// </summary>
        public double StabilityGrowthMultiplier { get; set; } = 1.0;

        /// <summary>Per-character location cap; null ⇒ use the world default.</summary>
        public int? MaxLocationsOverride { get; set; } = null;

        public MemoryProfile() { }
    }
}
