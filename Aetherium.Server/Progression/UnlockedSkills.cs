using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Progression
{
    /// <summary>The set of skill ids an actor has unlocked.</summary>
    public class UnlockedSkills : Component
    {
        private readonly HashSet<string> _ids = new();

        public IReadOnlyCollection<string> Ids => _ids;

        public bool Has(string skillId) => _ids.Contains(skillId);

        internal bool Add(string skillId) => _ids.Add(skillId);
    }
}
