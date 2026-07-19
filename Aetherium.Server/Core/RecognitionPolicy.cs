using System;

namespace Aetherium.Core
{
    /// <summary>
    /// Per-world individual-recognition policy (see OpenSpec change add-identity-recognition).
    /// Opt-in per world (disabled by default); carried on the ECS <see cref="World"/> as data and
    /// overridable via world generator parameters (Recognition*). Familiarity reuses the memory
    /// stability curve in <see cref="MemoryPolicy"/>, so social and spatial memory decay identically.
    /// </summary>
    public class RecognitionPolicy
    {
        /// <summary>Master switch: when false the proximity sweep does no work.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Recognition range in topology tiles (same z-level).</summary>
        public int RangeTiles { get; set; } = 6;

        /// <summary>Acuity when recognizing an individual of the recognizer's own kind.</summary>
        public double OwnKindAcuity { get; set; } = 0.9;

        /// <summary>Acuity when recognizing an individual of a different kind.</summary>
        public double OtherKindAcuity { get; set; } = 0.4;

        /// <summary>Recognition succeeds when acuity × effective familiarity ≥ this.</summary>
        public double RecognitionThreshold { get; set; } = 0.25;

        /// <summary>A pair apart longer than this begins a new encounter (re-fires the event).</summary>
        public double EncounterTimeoutSeconds { get; set; } = 300;

        /// <summary>Base familiarity half-life (before reinforcement grows a per-individual stability).</summary>
        public double FamiliarityHalfLifeSeconds { get; set; } = 86400;

        /// <summary>Familiarity strength assigned on first meeting an individual.</summary>
        public double MeetStrength { get; set; } = 0.5;

        /// <summary>Maximum individuals tracked per character before weakest-first pruning.</summary>
        public int MaxIndividuals { get; set; } = 1000;

        // --- Familiarity dynamics: the same shape as MemoryPolicy dynamics, applied to individuals. ---

        /// <summary>Factor familiarity stability multiplies by on each spaced re-meeting.</summary>
        public double StabilityGrowthFactor { get; set; } = 2.0;

        /// <summary>Minimum time since last seen before a re-meeting counts as spaced (grows stability).</summary>
        public double MinReinforcementIntervalSeconds { get; set; } = 60;

        /// <summary>Familiarity stability at/above which an individual becomes permanently known.</summary>
        public double PermanenceThresholdSeconds { get; set; } = 2592000;

        /// <summary>Own/other-kind acuity for a recognizer with no per-kind or profile override.</summary>
        public double AcuityFor(string recognizerKind, string targetKind) =>
            string.Equals(recognizerKind, targetKind, StringComparison.OrdinalIgnoreCase)
                ? OwnKindAcuity
                : OtherKindAcuity;

        /// <summary>Whether a given acuity × effective familiarity clears the recognition threshold.</summary>
        public bool Recognizes(double acuity, double effectiveFamiliarity) =>
            acuity * effectiveFamiliarity >= RecognitionThreshold;
    }
}
