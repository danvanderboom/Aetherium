using System.Collections.Generic;

namespace Aetherium.Server.Progression
{
    /// <summary>
    /// A skill/talent data asset (engine gap-analysis §4.4). References a future ability or
    /// attribute by stable string id rather than a typed reference — the ability system (§4.3)
    /// doesn't exist in this codebase yet; resolving these ids is a later system's job.
    /// </summary>
    public class SkillDefinition
    {
        public string Id { get; }
        public string Description { get; }

        /// <summary>Skill ids that must already be unlocked before this one can be. Empty for a root skill.</summary>
        public IReadOnlyList<string> Prerequisites { get; }

        /// <summary>Ability id this skill grants, if any.</summary>
        public string? UnlocksAbilityId { get; }

        /// <summary>Attribute name this skill modifies, if any (see <see cref="Attributes"/>).</summary>
        public string? ModifiesAttributeId { get; }
        public double ModifierAmount { get; }

        /// <summary>Optional XP gate: the actor's <see cref="RequiredPoolId"/> pool must be at least
        /// <see cref="RequiredLevel"/> to unlock. Null/zero means no level gate (prerequisites only).</summary>
        public string? RequiredPoolId { get; }
        public int RequiredLevel { get; }

        public SkillDefinition(string id, string description, IReadOnlyList<string>? prerequisites = null,
            string? unlocksAbilityId = null, string? modifiesAttributeId = null, double modifierAmount = 0,
            string? requiredPoolId = null, int requiredLevel = 0)
        {
            Id = id;
            Description = description;
            Prerequisites = prerequisites ?? System.Array.Empty<string>();
            UnlocksAbilityId = unlocksAbilityId;
            ModifiesAttributeId = modifiesAttributeId;
            ModifierAmount = modifierAmount;
            RequiredPoolId = requiredPoolId;
            RequiredLevel = requiredLevel;
        }
    }
}
