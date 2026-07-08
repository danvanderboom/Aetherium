using System;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>Flat + percent + minimum mitigation for one damage tag (engine gap-analysis §4.2).</summary>
    public readonly struct ResistanceEntry
    {
        public double Flat { get; }
        public double Percent { get; }
        public double Minimum { get; }

        public ResistanceEntry(double flat = 0, double percent = 0, double minimum = 0)
        {
            Flat = flat;
            Percent = percent;
            Minimum = minimum;
        }
    }

    /// <summary>
    /// Per-tag resistance stack an entity carries. Mitigation order is flat, then percent, then a
    /// minimum floor — applied per tag, in that stable order, as the design calls for.
    /// </summary>
    public class Resistances : Component
    {
        private readonly Dictionary<string, ResistanceEntry> _byTag = new();

        public void Set(string tag, ResistanceEntry entry) => _byTag[tag] = entry;

        public bool TryGet(string tag, out ResistanceEntry entry) => _byTag.TryGetValue(tag, out entry);

        /// <summary>Applies this entity's resistance for <paramref name="tag"/> to <paramref name="amount"/>.
        /// Never returns more than <paramref name="amount"/> (resistance cannot amplify damage) or less than zero.</summary>
        public double Mitigate(string tag, double amount)
        {
            if (!TryGet(tag, out var entry))
                return Math.Max(0, amount);

            double afterFlat = Math.Max(0, amount - entry.Flat);
            double afterPercent = afterFlat * (1 - entry.Percent);
            double floored = Math.Max(entry.Minimum, afterPercent);

            return Math.Clamp(floored, 0, amount);
        }
    }
}
