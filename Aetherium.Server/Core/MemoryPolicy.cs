using System;

namespace Aetherium.Core
{
    /// <summary>
    /// Per-world character-memory policy (see OpenSpec changes add-character-memory,
    /// add-memory-dynamics). Carried on the ECS <see cref="World"/> as data; overridable via world
    /// generator parameters (MemoryEnabled, MemoryMaxLocations, MemoryDecayHalfLifeSeconds, and the
    /// Memory* dynamics parameters below).
    /// </summary>
    public class MemoryPolicy
    {
        /// <summary>Whether perception-time memory recording is active for this world.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Maximum distinct locations tracked per character before oldest-first pruning.</summary>
        public int MaxLocations { get; set; } = 10000;

        /// <summary>
        /// Half-life for lazy strength decay, in (real-time) seconds. Zero or negative disables decay.
        /// With dynamics enabled this is the *base* half-life a memory decays at until reinforcement
        /// grows its own stability (a per-character <c>MemoryProfile</c> may scale it).
        /// </summary>
        public double DecayHalfLifeSeconds { get; set; } = 3600;

        // --- Dynamics (add-memory-dynamics): opt-in per world. Default off ⇒ exactly the Layer-1
        //     behavior — no stability written, no permanence, no culling, decay at DecayHalfLifeSeconds.

        /// <summary>Master switch for the reinforcement/permanence/forgetting model.</summary>
        public bool DynamicsEnabled { get; set; } = false;

        /// <summary>Factor a memory's stability multiplies by on each spaced reinforcement.</summary>
        public double StabilityGrowthFactor { get; set; } = 2.0;

        /// <summary>
        /// Minimum elapsed time since a memory was last seen before a re-encounter counts as spaced
        /// (and so grows stability). Massed re-exposure within this window bumps impressions and
        /// last-seen only — the spacing effect.
        /// </summary>
        public double MinReinforcementIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Stability at or above which a memory latches permanent and never decays. Default 30 days
        /// (≈10 spaced reinforcements from a 1-hour base at growth 2.0).
        /// </summary>
        public double PermanenceThresholdSeconds { get; set; } = 2592000;

        /// <summary>
        /// Effective strength below which a (non-permanent) memory is culled at write time. Zero or
        /// negative disables culling.
        /// </summary>
        public double ForgetThreshold { get; set; } = 0.05;

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

        /// <summary>
        /// Effective strength honoring a memory's own stability and permanence (add-memory-dynamics).
        /// A permanent memory never decays; a memory with no recorded stability (0) falls back to
        /// <paramref name="fallbackHalfLifeSeconds"/> (the character's effective base half-life), so
        /// legacy rows and dynamics-off worlds read exactly as the three-argument overload.
        /// </summary>
        public static double EffectiveStrength(double strength, TimeSpan age, double stabilitySeconds,
            bool permanent, double fallbackHalfLifeSeconds)
        {
            if (permanent)
                return strength;
            var halfLife = stabilitySeconds > 0 ? stabilitySeconds : fallbackHalfLifeSeconds;
            return EffectiveStrength(strength, age, halfLife);
        }

        /// <summary>
        /// Grows a memory's stability on a spaced reinforcement: an un-reinforced memory (stability 0)
        /// starts from <paramref name="baseHalfLifeSeconds"/>, then each reinforcement multiplies by
        /// <paramref name="growthFactor"/>. Geometric growth is the shape spaced-repetition systems
        /// converge on; the caller latches permanence and refreshes strength.
        /// </summary>
        public static double ReinforceStability(double currentStability, double baseHalfLifeSeconds,
            double growthFactor)
        {
            var basis = currentStability > 0 ? currentStability : baseHalfLifeSeconds;
            return basis * growthFactor;
        }

        /// <summary>
        /// Resolves the effective per-character dynamics parameters for one recording pass, folding a
        /// character's <c>MemoryProfile</c> multipliers into the world policy. When dynamics are
        /// disabled the profile is ignored entirely (legacy behavior is byte-identical), and the
        /// returned snapshot carries <see cref="MemoryDynamics.Enabled"/> = false.
        /// </summary>
        public MemoryDynamics ResolveDynamics(double halfLifeMultiplier = 1.0,
            double stabilityGrowthMultiplier = 1.0, int? maxLocationsOverride = null)
        {
            if (!DynamicsEnabled)
            {
                return new MemoryDynamics
                {
                    Enabled = false,
                    BaseHalfLifeSeconds = DecayHalfLifeSeconds,
                    StabilityGrowthFactor = StabilityGrowthFactor,
                    MinReinforcementIntervalSeconds = MinReinforcementIntervalSeconds,
                    PermanenceThresholdSeconds = PermanenceThresholdSeconds,
                    ForgetThreshold = 0,
                    MaxLocations = MaxLocations,
                };
            }

            return new MemoryDynamics
            {
                Enabled = true,
                BaseHalfLifeSeconds = DecayHalfLifeSeconds * halfLifeMultiplier,
                StabilityGrowthFactor = StabilityGrowthFactor * stabilityGrowthMultiplier,
                MinReinforcementIntervalSeconds = MinReinforcementIntervalSeconds,
                PermanenceThresholdSeconds = PermanenceThresholdSeconds,
                ForgetThreshold = ForgetThreshold,
                MaxLocations = maxLocationsOverride ?? MaxLocations,
            };
        }
    }

    /// <summary>
    /// A resolved, per-character snapshot of memory dynamics for a single recording pass
    /// (add-memory-dynamics) — world policy with the character's <c>MemoryProfile</c> multipliers
    /// already folded in. A default-valued instance (<see cref="Enabled"/> = false) drives the exact
    /// legacy path. Passed to <c>Memory.AddMemory</c>/<c>Remember</c> and used for write-time culling.
    /// </summary>
    public readonly struct MemoryDynamics
    {
        /// <summary>When false, reinforcement/permanence/culling are all skipped (legacy behavior).</summary>
        public bool Enabled { get; init; }

        /// <summary>Effective base half-life (world half-life × profile multiplier) for fallback decay
        /// and the starting point of stability growth.</summary>
        public double BaseHalfLifeSeconds { get; init; }

        public double StabilityGrowthFactor { get; init; }
        public double MinReinforcementIntervalSeconds { get; init; }
        public double PermanenceThresholdSeconds { get; init; }

        /// <summary>Effective strength below which non-permanent memories cull; 0 disables culling.</summary>
        public double ForgetThreshold { get; init; }

        /// <summary>Effective per-character location cap.</summary>
        public int MaxLocations { get; init; }
    }
}
