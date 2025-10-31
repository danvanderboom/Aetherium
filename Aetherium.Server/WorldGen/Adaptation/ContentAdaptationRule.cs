using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Rule for adapting content based on agent behavior.
    /// </summary>
    public sealed class ContentAdaptationRule
    {
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Priority { get; set; } = 0.5;

        /// <summary>
        /// Type of content adaptation: "loot", "monster_density", "puzzle_difficulty", "hints"
        /// </summary>
        public string ContentType { get; set; } = "loot";

        /// <summary>
        /// Conditions that must be met for this rule to apply.
        /// </summary>
        public ContentAdaptationCondition Conditions { get; set; } = new ContentAdaptationCondition();

        /// <summary>
        /// Actions to take when rule applies.
        /// </summary>
        public ContentAdaptationAction Actions { get; set; } = new ContentAdaptationAction();

        /// <summary>
        /// Checks if this rule applies to the given behavior and content needs.
        /// </summary>
        public bool AppliesTo(BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            // Check content type match
            if (!string.IsNullOrEmpty(Conditions.RequiredContentType) && ContentType != Conditions.RequiredContentType)
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

            // Check required need types
            if (Conditions.RequiredNeedTypes?.Count > 0)
            {
                var needTypes = contentNeeds.Select(n => n.NeedType).ToList();
                if (!Conditions.RequiredNeedTypes.Any(nt => needTypes.Contains(nt)))
                {
                    return false;
                }
            }

            // Check struggle count
            if (Conditions.MinStruggleCount > 0)
            {
                if (behaviorAnalysis.StrugglePatterns.Count < Conditions.MinStruggleCount)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Conditions for content adaptation rules.
    /// </summary>
    public sealed class ContentAdaptationCondition
    {
        public string? RequiredContentType { get; set; }
        public double MinWeaknessPriority { get; set; }
        public List<string>? RequiredNeedTypes { get; set; }
        public int MinStruggleCount { get; set; }
        public double? MaxSuccessRate { get; set; } // Maximum success rate threshold
    }

    /// <summary>
    /// Actions for content adaptation rules.
    /// </summary>
    public sealed class ContentAdaptationAction
    {
        public string ActionType { get; set; } = "inject"; // "inject", "adjust", "remove"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}

