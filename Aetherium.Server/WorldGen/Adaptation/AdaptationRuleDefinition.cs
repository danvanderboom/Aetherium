using System;
using System.Collections.Generic;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Definition of an adaptation rule loaded from JSON configuration.
    /// </summary>
    public sealed class AdaptationRuleDefinition
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RuleType { get; set; } = "quest"; // "quest", "content", "narrative"
        public double Priority { get; set; } = 0.5;

        /// <summary>
        /// Conditions that must be met for this rule to apply.
        /// </summary>
        public RuleConditionDefinition Conditions { get; set; } = new RuleConditionDefinition();

        /// <summary>
        /// Actions to take when rule applies.
        /// </summary>
        public RuleActionDefinition Actions { get; set; } = new RuleActionDefinition();

        /// <summary>
        /// Converts this definition to a QuestAdaptationRule.
        /// </summary>
        public QuestAdaptationRule ToQuestRule()
        {
            return new QuestAdaptationRule
            {
                RuleId = RuleId,
                Name = Name,
                Description = Description,
                Priority = Priority,
                Conditions = new QuestAdaptationCondition
                {
                    MinWeaknessPriority = Conditions.MinWeaknessPriority,
                    RequiredWeaknessTypes = Conditions.RequiredWeaknessTypes,
                    MinStruggleCount = Conditions.MinStruggleCount,
                    MinSuccessRate = Conditions.MinSuccessRate,
                    RequiredActionTypes = Conditions.RequiredActionTypes
                },
                Actions = new QuestAdaptationAction
                {
                    ActionType = Actions.ActionType ?? "generate",
                    QuestTemplateId = Actions.TemplateId ?? string.Empty,
                    Parameters = Actions.Parameters ?? new Dictionary<string, object>()
                }
            };
        }

        /// <summary>
        /// Converts this definition to a ContentAdaptationRule.
        /// </summary>
        public ContentAdaptationRule ToContentRule()
        {
            return new ContentAdaptationRule
            {
                RuleId = RuleId,
                Name = Name,
                Description = Description,
                Priority = Priority,
                ContentType = Actions.ContentType ?? "loot",
                Conditions = new ContentAdaptationCondition
                {
                    RequiredContentType = Conditions.RequiredContentType,
                    MinWeaknessPriority = Conditions.MinWeaknessPriority,
                    RequiredNeedTypes = Conditions.RequiredNeedTypes,
                    MinStruggleCount = Conditions.MinStruggleCount,
                    MaxSuccessRate = Conditions.MaxSuccessRate
                },
                Actions = new ContentAdaptationAction
                {
                    ActionType = Actions.ActionType ?? "inject",
                    Parameters = Actions.Parameters ?? new Dictionary<string, object>()
                }
            };
        }

        /// <summary>
        /// Converts this definition to a NarrativeAdaptationRule.
        /// </summary>
        public NarrativeAdaptationRule ToNarrativeRule()
        {
            return new NarrativeAdaptationRule
            {
                RuleId = RuleId,
                Name = Name,
                Description = Description,
                Priority = Priority,
                NarrativeType = Actions.NarrativeType ?? "tokens",
                Conditions = new NarrativeAdaptationCondition
                {
                    RequiredNarrativeType = Conditions.RequiredNarrativeType,
                    MinWeaknessPriority = Conditions.MinWeaknessPriority,
                    RequireHighInterest = Conditions.RequireHighInterest,
                    MinInteractionCount = Conditions.MinInteractionCount
                },
                Actions = new NarrativeAdaptationAction
                {
                    ActionType = Actions.ActionType ?? "add",
                    Parameters = Actions.Parameters ?? new Dictionary<string, object>()
                }
            };
        }
    }

    /// <summary>
    /// Condition definition for adaptation rules.
    /// </summary>
    public sealed class RuleConditionDefinition
    {
        public string? RequiredContentType { get; set; }
        public string? RequiredNarrativeType { get; set; }
        public double MinWeaknessPriority { get; set; }
        public List<string>? RequiredWeaknessTypes { get; set; }
        public List<string>? RequiredNeedTypes { get; set; }
        public int MinStruggleCount { get; set; }
        public double? MinSuccessRate { get; set; }
        public double? MaxSuccessRate { get; set; }
        public List<string>? RequiredActionTypes { get; set; }
        public bool RequireHighInterest { get; set; }
        public int MinInteractionCount { get; set; }
    }

    /// <summary>
    /// Action definition for adaptation rules.
    /// </summary>
    public sealed class RuleActionDefinition
    {
        public string? ActionType { get; set; }
        public string? TemplateId { get; set; }
        public string? ContentType { get; set; }
        public string? NarrativeType { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }
}

