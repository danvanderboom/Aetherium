using System.Collections.Generic;

namespace Aetherium.Server.Progression
{
    /// <summary>Registry of <see cref="SkillDefinition"/>s by id — the tree/web/point-buy structure
    /// is expressed entirely through each definition's <see cref="SkillDefinition.Prerequisites"/>,
    /// not by this catalog's own shape.</summary>
    public class SkillCatalog
    {
        private readonly Dictionary<string, SkillDefinition> _skills = new();

        public bool Add(SkillDefinition skill) => _skills.TryAdd(skill.Id, skill);

        public bool TryGet(string id, out SkillDefinition? skill) => _skills.TryGetValue(id, out skill);
    }
}
