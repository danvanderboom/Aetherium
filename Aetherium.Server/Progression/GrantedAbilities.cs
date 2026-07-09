using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Progression
{
    /// <summary>
    /// The set of ability ids a player has been granted (engine gap-analysis §4.4), populated by a
    /// skill's <c>UnlocksAbilityId</c> when unlocked. Enforced as a cast gate only when the world's
    /// <c>RequireSkillToCastAbilities</c> is true; otherwise catalog membership is the sole gate and
    /// this component simply records what was granted.
    /// </summary>
    public class GrantedAbilities : Component
    {
        private readonly HashSet<string> _ids = new();

        public IReadOnlyCollection<string> Ids => _ids;

        public bool Has(string abilityId) => _ids.Contains(abilityId);

        public bool Grant(string abilityId) => _ids.Add(abilityId);
    }
}
