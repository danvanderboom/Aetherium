using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Rule for adapting quests based on agent behavior.
    /// </summary>
    public sealed class QuestAdaptationRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Priority { get; set; } = 0.5; // 0.0 to 1.0

        /// <summary>
        /// Conditions that must be met for this rule to apply.
        /// </summary>
        public QuestAdaptationCondition Conditions { get; set; } = new QuestAdaptationCondition();

        /// <summary>
        /// Actions to take when rule applies.
        /// </summary>
        public QuestAdaptationAction Actions { get; set; } = new QuestAdaptationAction();

        /// <summary>
        /// Checks if this rule applies to the given behavior and content needs.
        /// </summary>
        public bool AppliesTo(BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            // Check conditions
            if (Conditions.MinWeaknessPriority > 0)
            {
                var maxPriority = contentNeeds.Any() ? contentNeeds.Max(n => n.Priority) : 0;
                if (maxPriority < Conditions.MinWeaknessPriority)
                {
                    return false;
                }
            }

            if (Conditions.RequiredWeaknessTypes?.Count > 0)
            {
                var weaknessTypes = contentNeeds.Select(n => n.NeedType).ToList();
                if (!Conditions.RequiredWeaknessTypes.Any(wt => weaknessTypes.Contains(wt)))
                {
                    return false;
                }
            }

            if (Conditions.MinStruggleCount > 0)
            {
                if (behaviorAnalysis.StrugglePatterns.Count < Conditions.MinStruggleCount)
                {
                    return false;
                }
            }

            if (Conditions.MinSuccessRate != null)
            {
                // Calculate overall success rate from action patterns
                var totalActions = behaviorAnalysis.ActionPatterns.Sum(p => p.TotalCount);
                var successfulActions = behaviorAnalysis.ActionPatterns.Sum(p => p.SuccessCount);
                var successRate = totalActions > 0 ? (double)successfulActions / totalActions : 0.0;

                if (successRate < Conditions.MinSuccessRate)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies this rule to generate or adapt a quest.
        /// </summary>
        public QuestDefinition Apply(QuestDefinition? existingQuest, BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            if (existingQuest != null && Actions.ActionType == "adapt")
            {
                return AdaptiveQuestGenerator.AdaptQuest(existingQuest, behaviorAnalysis, contentNeeds);
            }
            else if (Actions.ActionType == "generate")
            {
                var questId = existingQuest?.QuestId ?? Guid.NewGuid().ToString("N");
                return AdaptiveQuestGenerator.GenerateQuest(questId, behaviorAnalysis, contentNeeds);
            }

            return existingQuest ?? new QuestDefinition();
        }
    }

    /// <summary>
    /// Conditions for quest adaptation rules.
    /// </summary>
    public sealed class QuestAdaptationCondition
    {
        public double? MinWeaknessPriority { get; set; }
        public List<string>? RequiredWeaknessTypes { get; set; }
        public int MinStruggleCount { get; set; }
        public double? MinSuccessRate { get; set; } // Minimum success rate threshold
        public List<string>? RequiredActionTypes { get; set; }
    }

    /// <summary>
    /// Actions for quest adaptation rules.
    /// </summary>
    public sealed class QuestAdaptationAction
    {
        public string ActionType { get; set; } = "generate"; // "generate" or "adapt"
        public string QuestTemplateId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}

