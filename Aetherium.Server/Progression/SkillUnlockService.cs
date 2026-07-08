using System.Linq;

namespace Aetherium.Server.Progression
{
    public enum SkillUnlockResult
    {
        Unlocked,
        AlreadyUnlocked,
        UnknownSkill,
        PrerequisitesNotMet,
    }

    /// <summary>Stateless gating service for unlocking a skill against its prerequisites — mirrors
    /// this codebase's existing pure-service pattern (<c>CombatSystem</c>, <c>DamagePipeline</c>).</summary>
    public class SkillUnlockService
    {
        public SkillUnlockResult TryUnlock(UnlockedSkills unlocked, SkillCatalog catalog, string skillId)
        {
            if (unlocked.Has(skillId))
                return SkillUnlockResult.AlreadyUnlocked;

            if (!catalog.TryGet(skillId, out var skill) || skill is null)
                return SkillUnlockResult.UnknownSkill;

            if (!skill.Prerequisites.All(unlocked.Has))
                return SkillUnlockResult.PrerequisitesNotMet;

            unlocked.Add(skillId);
            return SkillUnlockResult.Unlocked;
        }
    }
}
