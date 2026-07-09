using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>
    /// A per-target threat/aggro ledger (engine gap-analysis §4.2): held by the defender, crediting
    /// each attacker's cumulative threat. Simple top-of-list heuristic by default; NPC AI (§4.5) may
    /// override with its own targeting rule using this same ledger.
    /// </summary>
    public class ThreatTable : Component
    {
        private readonly Dictionary<string, double> _byAttacker = new();

        public IReadOnlyDictionary<string, double> ThreatByAttacker => _byAttacker;

        public void AddThreat(string attackerEntityId, double amount)
        {
            _byAttacker[attackerEntityId] = _byAttacker.GetValueOrDefault(attackerEntityId, 0) + amount;
        }

        /// <summary>The attacker with the highest cumulative threat, or null if the ledger is empty.</summary>
        public string? GetTopThreat()
            => _byAttacker.Count == 0 ? null : _byAttacker.OrderByDescending(kv => kv.Value).First().Key;
    }
}
