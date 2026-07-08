using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Progression
{
    /// <summary>
    /// A per-campaign named attribute vector (engine gap-analysis §4.4). String-keyed rather than
    /// a fixed field list, so a campaign can define <c>Strength</c>, <c>Hacking</c>, or <c>Piety</c>
    /// without an engine code change. <see cref="Vitality"/>/<see cref="Speed"/> are the engine's
    /// own shipped defaults (Phase 2 decides whether/how they drive <c>Health</c>/<c>ActionSpeed</c>).
    /// </summary>
    public class Attributes : Component
    {
        public const string Vitality = "vitality";
        public const string Speed = "speed";

        private readonly Dictionary<string, double> _values = new();

        public double Get(string name, double defaultValue = 0)
            => _values.TryGetValue(name, out var value) ? value : defaultValue;

        public void Set(string name, double value) => _values[name] = value;

        public bool Has(string name) => _values.ContainsKey(name);

        /// <summary>Every named attribute this actor carries — used by the read accessor.</summary>
        public IReadOnlyDictionary<string, double> Values => _values;
    }
}
