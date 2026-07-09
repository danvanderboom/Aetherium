using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Progression
{
    /// <summary>How a progress pool's cumulative XP maps to a level. Closed set this slice; the
    /// server compiler turns each into a runtime <c>ILevelCurve</c>.</summary>
    [GenerateSerializer]
    public enum LevelCurveKind
    {
        Linear,
    }

    /// <summary>Declarative level curve for a pool (engine gap-analysis §4.4). Data only — compiled to
    /// a runtime <c>ILevelCurve</c> by the server's <c>ProgressionCompiler</c>.</summary>
    [GenerateSerializer]
    public class LevelCurveDefinition
    {
        [Id(0)] public LevelCurveKind Kind { get; set; } = LevelCurveKind.Linear;
        /// <summary>XP per level for <see cref="LevelCurveKind.Linear"/> (level = 1 + floor(xp / this)).</summary>
        [Id(1)] public double XpPerLevel { get; set; } = 100;
    }

    /// <summary>Declarative definition of one XP/level track a world's characters start with.</summary>
    [GenerateSerializer]
    public class ProgressPoolDefinition
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public LevelCurveDefinition Curve { get; set; } = new();
        [Id(2)] public double StartingXp { get; set; }
        [Id(3)] public int StartingLevel { get; set; } = 1;
    }

    /// <summary>Serializable mirror of the runtime <c>SkillDefinition</c> (which isn't serializable and
    /// is a Server type). The tree/web/point-buy structure is expressed entirely through
    /// <see cref="Prerequisites"/>; <see cref="RequiredPoolId"/>/<see cref="RequiredLevel"/> add an
    /// optional XP gate so leveling actually unlocks skills.</summary>
    [GenerateSerializer]
    public class SkillDefinitionData
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string Description { get; set; } = string.Empty;
        [Id(2)] public List<string> Prerequisites { get; set; } = new();

        /// <summary>Ability id this skill grants when unlocked (added to the actor's granted set).</summary>
        [Id(3)] public string? UnlocksAbilityId { get; set; }

        /// <summary>Attribute this skill changes when unlocked, by <see cref="ModifierAmount"/>.</summary>
        [Id(4)] public string? ModifiesAttributeId { get; set; }
        [Id(5)] public double ModifierAmount { get; set; }

        /// <summary>Optional XP gate: the actor's <see cref="RequiredPoolId"/> pool must be at least
        /// <see cref="RequiredLevel"/> to unlock. Zero/null means no level gate (prerequisites only).</summary>
        [Id(6)] public string? RequiredPoolId { get; set; }
        [Id(7)] public int RequiredLevel { get; set; }
    }

    /// <summary>The event that triggers an <see cref="XpAwardRule"/>. Only monster defeat this slice;
    /// exploration/quest/crafting triggers are additive later.</summary>
    [GenerateSerializer]
    public enum XpAwardEvent
    {
        MonsterDefeated,
    }

    /// <summary>Declarative "on this event, award this XP to this pool" rule (engine gap-analysis §4.4).
    /// A typed subset now; the general condition→action form is the ECA seam (design-eca-visual-scripting).</summary>
    [GenerateSerializer]
    public class XpAwardRule
    {
        [Id(0)] public XpAwardEvent OnEvent { get; set; }
        [Id(1)] public string PoolId { get; set; } = string.Empty;
        [Id(2)] public double Amount { get; set; }
        /// <summary>Optional: only award when the defeated entity's type name matches (case-insensitive).</summary>
        [Id(3)] public string? EnemyTypeFilter { get; set; }
    }

    /// <summary>A derived stat an <see cref="AttributeDerivation"/> can drive.</summary>
    [GenerateSerializer]
    public enum DerivedStat
    {
        HealthMax,
        ActionSpeed,
    }

    /// <summary>Declarative mapping of an attribute onto a derived component stat, as
    /// <c>Base + PerPoint × attributeValue</c> (engine gap-analysis §4.4). Applied on change events
    /// (join, skill-modify), not polled per tick.</summary>
    [GenerateSerializer]
    public class AttributeDerivation
    {
        [Id(0)] public string AttributeId { get; set; } = string.Empty;
        [Id(1)] public DerivedStat DerivedStat { get; set; }
        [Id(2)] public double PerPoint { get; set; } = 1;
        [Id(3)] public double Base { get; set; }
    }

    /// <summary>
    /// A world's character-progression content (engine gap-analysis §4.4): the XP pools its
    /// characters start with, the skill catalog, starting attributes/role-affinity, the XP-award
    /// rules, the attribute→stat derivations, and whether abilities must be skill-unlocked to cast.
    /// Threaded through world creation exactly like <c>DeathPolicy</c>/<c>AbilityConfig</c>; null
    /// anywhere means the world has no progression. The engine ships none — it's campaign data.
    /// </summary>
    [GenerateSerializer]
    public class ProgressionConfig
    {
        [Id(0)] public List<ProgressPoolDefinition> Pools { get; set; } = new();
        [Id(1)] public List<SkillDefinitionData> Skills { get; set; } = new();
        [Id(2)] public Dictionary<string, double> StartingAttributes { get; set; } = new();
        [Id(3)] public Dictionary<string, double> StartingRoleAffinity { get; set; } = new();
        [Id(4)] public List<XpAwardRule> XpAwardRules { get; set; } = new();
        [Id(5)] public List<AttributeDerivation> AttributeDerivations { get; set; } = new();

        /// <summary>When true, a player may only cast an ability they've been granted (via a skill's
        /// <see cref="SkillDefinitionData.UnlocksAbilityId"/>). When false (default), catalog
        /// membership is the sole ability gate — preserving the pre-progression behavior.</summary>
        [Id(6)] public bool RequireSkillToCastAbilities { get; set; }
    }
}
