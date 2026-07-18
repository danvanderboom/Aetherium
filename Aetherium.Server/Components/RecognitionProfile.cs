using System;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Per-character overrides of the world's recognition policy (add-identity-recognition) — how well
    /// this individual tells others apart, as data. Absent component ⇒ world defaults. Only the
    /// enabled flag, range, and acuity are overridable per character; reinforcement/permanence/
    /// half-life/threshold stay world-level. Consulted only when the world's
    /// <c>RecognitionPolicy.Enabled</c> is true.
    /// </summary>
    public class RecognitionProfile : Component
    {
        /// <summary>Whether this character participates as a recognizer; null ⇒ world default (on).</summary>
        public bool? EnabledOverride { get; set; } = null;

        /// <summary>Recognition range override in topology tiles; null ⇒ world default.</summary>
        public int? RangeTilesOverride { get; set; } = null;

        /// <summary>Own-kind acuity override; null ⇒ world default.</summary>
        public double? OwnKindAcuityOverride { get; set; } = null;

        /// <summary>Other-kind acuity override; null ⇒ world default.</summary>
        public double? OtherKindAcuityOverride { get; set; } = null;

        /// <summary>Acuity for specific target kinds, taking precedence over own/other-kind values.</summary>
        public Dictionary<string, double> PerKindAcuity { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public RecognitionProfile() { }

        /// <summary>
        /// Resolves this character's acuity toward a target kind: a per-kind override wins, otherwise
        /// the own/other-kind override, otherwise the world policy default.
        /// </summary>
        public double AcuityFor(string recognizerKind, string targetKind, RecognitionPolicy policy)
        {
            if (PerKindAcuity != null && PerKindAcuity.TryGetValue(targetKind, out var perKind))
                return perKind;
            bool ownKind = string.Equals(recognizerKind, targetKind, StringComparison.OrdinalIgnoreCase);
            return ownKind
                ? (OwnKindAcuityOverride ?? policy.OwnKindAcuity)
                : (OtherKindAcuityOverride ?? policy.OtherKindAcuity);
        }
    }
}
