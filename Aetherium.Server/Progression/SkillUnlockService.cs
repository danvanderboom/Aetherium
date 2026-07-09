using System.Linq;

namespace Aetherium.Server.Progression
{
    public enum SkillUnlockResult
    {
        Unlocked,
        AlreadyUnlocked,
        UnknownSkill,
        PrerequisitesNotMet,
        PoolLevelTooLow,
    }

    /// <summary>Stateless gating service for unlocking a skill against its prerequisites — mirrors
    /// this codebase's existing pure-service pattern (<c>CombatSystem</c>, <c>DamagePipeline</c>).</summary>
    public class SkillUnlockService
    {
        /// <summary>Attempts to unlock <paramref name="skillId"/>. Gates on: not already unlocked →
        /// known → all prerequisites unlocked → (if the skill declares one) the actor's required pool
        /// at or above the required level. <paramref name="pools"/> is optional — a skill with a level
        /// requirement fails when it's absent.</summary>
        public SkillUnlockResult TryUnlock(UnlockedSkills unlocked, SkillCatalog catalog, string skillId,
            ProgressPools? pools = null)
        {
            if (unlocked.Has(skillId))
                return SkillUnlockResult.AlreadyUnlocked;

            if (!catalog.TryGet(skillId, out var skill) || skill is null)
                return SkillUnlockResult.UnknownSkill;

            if (!skill.Prerequisites.All(unlocked.Has))
                return SkillUnlockResult.PrerequisitesNotMet;

            if (!string.IsNullOrEmpty(skill.RequiredPoolId) && skill.RequiredLevel > 0)
            {
                int level = pools is not null && pools.Pools.TryGetValue(skill.RequiredPoolId!, out var pool)
                    ? pool.Level
                    : 0;
                if (level < skill.RequiredLevel)
                    return SkillUnlockResult.PoolLevelTooLow;
            }

            unlocked.Add(skillId);
            return SkillUnlockResult.Unlocked;
        }
    }
}
