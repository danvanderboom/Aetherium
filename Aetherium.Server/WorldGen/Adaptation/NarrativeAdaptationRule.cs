using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;
using Aetherium.WorldGen;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Rule for adapting narrative elements based on agent behavior.
    /// </summary>
    public sealed class NarrativeAdaptationRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Priority { get; set; } = 0.5;

        /// <summary>
        /// Type of narrative adaptation: "tokens", "pois", "difficulty", "description"
        /// </summary>
        public string NarrativeType { get; set; } = "tokens";

        /// <summary>
        /// Conditions that must be met for this rule to apply.
        /// </summary>
        public NarrativeAdaptationCondition Conditions { get; set; } = new NarrativeAdaptationCondition();

        /// <summary>
        /// Actions to take when rule applies.
        /// </summary>
        public NarrativeAdaptationAction Actions { get; set; } = new NarrativeAdaptationAction();

        /// <summary>
        /// Applies this rule to adapt narrative constraints.
        /// </summary>
        public NarrativeGenerationConstraints Apply(
            NarrativeGenerationConstraints baseConstraints,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var adapted = AdaptiveNarrativeGenerator.AdaptNarrativeConstraints(
                baseConstraints,
                behaviorAnalysis,
                contentNeeds);

            // Apply rule-specific adaptations
            switch (NarrativeType)
            {
                case "tokens":
                    var tokens = AdaptiveNarrativeGenerator.GenerateNarrativeTokens(behaviorAnalysis);
                    adapted.Tokens.AddRange(tokens);
                    break;

                case "pois":
                    var pois = AdaptiveNarrativeGenerator.GenerateNarrativePOIs(behaviorAnalysis);
                    adapted.RequiredPoints.AddRange(pois);
                    break;

                case "difficulty":
                    var adaptedConstraints = AdaptiveNarrativeGenerator.AdaptNarrativeConstraints(
                        baseConstraints,
                        behaviorAnalysis,
                        contentNeeds);
                    foreach (var kvp in adaptedConstraints.DifficultyByDepth)
                    {
                        adapted.DifficultyByDepth[kvp.Key] = kvp.Value;
                    }
                    break;
            }

            return adapted;
        }

        /// <summary>
        /// Checks if this rule applies to the given behavior and content needs.
        /// </summary>
        public bool AppliesTo(BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            // Check narrative type match
            if (!string.IsNullOrEmpty(Conditions.RequiredNarrativeType) && NarrativeType != Conditions.RequiredNarrativeType)
            {
                return false;
            }

            // Check weakness priority
            if (Conditions.MinWeaknessPriority > 0)
            {
                var maxPriority = contentNeeds.Any() ? contentNeeds.Max(n => n.Priority) : 0;
                if (maxPriority < Conditions.MinWeaknessPriority)
                {
                    return false;
                }
            }

            // Check interest requirements
            if (Conditions.RequireHighInterest && behaviorAnalysis.InteractionPatterns.Count == 0)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Conditions for narrative adaptation rules.
    /// </summary>
    public sealed class NarrativeAdaptationCondition
    {
        public string? RequiredNarrativeType { get; set; }
        public double MinWeaknessPriority { get; set; }
        public bool RequireHighInterest { get; set; }
        public int MinInteractionCount { get; set; }
    }

    /// <summary>
    /// Actions for narrative adaptation rules.
    /// </summary>
    public sealed class NarrativeAdaptationAction
    {
        public string ActionType { get; set; } = "add"; // "add", "modify", "remove"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}

