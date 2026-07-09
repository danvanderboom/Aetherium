using System.Collections.Generic;

namespace Aetherium.Server.Ai
{
    /// <summary>
    /// Per-NPC scratch storage a behavior tree reads/writes across ticks (engine gap-analysis
    /// §4.5). Deliberately a plain key/value store, not filtered Perception — wiring real
    /// perception into a blackboard is a Phase 2 concern once a caller needs it.
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object?> _values = new();

        public void Set<T>(string key, T value) => _values[key] = value;

        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default!;
            return false;
        }

        public bool Has(string key) => _values.ContainsKey(key);

        public void Clear(string key) => _values.Remove(key);
    }
}
