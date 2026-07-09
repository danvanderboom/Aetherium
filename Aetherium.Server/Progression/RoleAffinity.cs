using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Progression
{
    /// <summary>Optional per-actor `{roleTag: weight}` bias (engine gap-analysis §4.4) — empty for a
    /// freeform build, a dominant weight for a fixed archetype.</summary>
    public class RoleAffinity : Component
    {
        private readonly Dictionary<string, double> _weights = new();

        public double Get(string roleTag, double defaultValue = 0)
            => _weights.TryGetValue(roleTag, out var value) ? value : defaultValue;

        public void Set(string roleTag, double weight) => _weights[roleTag] = weight;
    }
}
