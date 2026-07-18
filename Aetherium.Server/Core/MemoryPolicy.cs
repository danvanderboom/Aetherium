using System;

namespace Aetherium.Core
{
    /// <summary>
    /// Per-world character-memory policy (see OpenSpec change add-character-memory).
    /// Carried on the ECS <see cref="World"/> as data; overridable via world generator
    /// parameters (MemoryEnabled, MemoryMaxLocations, MemoryDecayHalfLifeSeconds).
    /// </summary>
    public class MemoryPolicy
    {
        /// <summary>Whether perception-time memory recording is active for this world.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Maximum distinct locations tracked per character before oldest-first pruning.</summary>
        public int MaxLocations { get; set; } = 10000;

        /// <summary>
        /// Half-life for lazy strength decay, in (real-time) seconds. Zero or negative disables decay.
        /// </summary>
        public double DecayHalfLifeSeconds { get; set; } = 3600;

        /// <summary>
        /// Effective strength after lazy decay: strength halves once per elapsed half-life.
        /// Pure function; reads never mutate stored memory state.
        /// </summary>
        public static double EffectiveStrength(double strength, TimeSpan age, double halfLifeSeconds)
        {
            if (halfLifeSeconds <= 0)
                return strength;
            return strength * Math.Pow(0.5, age.TotalSeconds / halfLifeSeconds);
        }
    }
}
