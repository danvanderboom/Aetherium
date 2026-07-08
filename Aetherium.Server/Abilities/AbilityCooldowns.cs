using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Server.Abilities
{
    /// <summary>
    /// A per-caster ability-cooldown ledger (engine gap-analysis §4.3): remaining ticks before each
    /// ability id can be cast again. Per-caster (not a field on the shared <c>Ability</c> template)
    /// because a template is cast by many actors — resolves the open question left by add-abilities.
    /// Created lazily on first cast; ticked down each world tick.
    /// </summary>
    public class AbilityCooldowns : Component
    {
        private readonly Dictionary<string, int> _remaining = new();

        /// <summary>True while <paramref name="abilityId"/> still has cooldown ticks remaining.</summary>
        public bool IsOnCooldown(string abilityId)
            => _remaining.TryGetValue(abilityId, out var ticks) && ticks > 0;

        /// <summary>Puts <paramref name="abilityId"/> on cooldown for <paramref name="ticks"/> ticks
        /// (no-op for a non-positive duration).</summary>
        public void SetCooldown(string abilityId, int ticks)
        {
            if (ticks > 0)
                _remaining[abilityId] = ticks;
        }

        public int RemainingTicks(string abilityId)
            => _remaining.TryGetValue(abilityId, out var ticks) ? ticks : 0;

        /// <summary>Advances every cooldown by one tick, dropping any that reach zero.</summary>
        public void Tick()
        {
            foreach (var id in _remaining.Keys.ToList())
            {
                var next = _remaining[id] - 1;
                if (next <= 0)
                    _remaining.Remove(id);
                else
                    _remaining[id] = next;
            }
        }

        public IReadOnlyDictionary<string, int> Snapshot => _remaining;
    }
}
