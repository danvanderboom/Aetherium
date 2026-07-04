using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.WorldGen;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Engine for loading and executing adaptation rules.
    /// </summary>
    public static class AdaptationRuleEngine
    {
        private static readonly List<AdaptationRuleDefinition> _rules = new List<AdaptationRuleDefinition>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Loads adaptation rules from JSON files in the specified directory.
        /// </summary>
        public static void LoadRules(string rulesDirectory)
        {
            if (!Directory.Exists(rulesDirectory))
            {
                Directory.CreateDirectory(rulesDirectory);
                return;
            }

            var ruleFiles = Directory.GetFiles(rulesDirectory, "*.json");

            lock (_lock)
            {
                _rules.Clear();

                foreach (var file in ruleFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var rule = JsonSerializer.Deserialize<AdaptationRuleDefinition>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (rule != null)
                        {
                            _rules.Add(rule);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AdaptationRuleEngine] Error loading rule from {file}: {ex.Message}");
                    }
                }

                // Sort by priority (highest first)
                _rules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        /// <summary>
        /// Gets all rules that apply to the given behavior and content needs.
        /// </summary>
        public static List<AdaptationRuleDefinition> GetApplicableRules(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            lock (_lock)
            {
                return _rules
                    .Where(rule => DoesRuleApply(rule, behaviorAnalysis, contentNeeds))
                    .ToList();
            }
        }

        /// <summary>
        /// Executes applicable rules and returns the results.
        /// </summary>
        public static List<RuleExecutionResult> ExecuteRules(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            NarrativeGenerationConstraints? baseConstraints = null)
        {
            var results = new List<RuleExecutionResult>();

            var applicableRules = GetApplicableRules(behaviorAnalysis, contentNeeds);

            foreach (var ruleDef in applicableRules)
            {
                try
                {
                    var result = ExecuteRule(ruleDef, behaviorAnalysis, contentNeeds, baseConstraints);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new RuleExecutionResult
                    {
                        RuleId = ruleDef.RuleId,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return results;
        }

        private static bool DoesRuleApply(
            AdaptationRuleDefinition rule,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            // Check rule type-specific application
            switch (rule.RuleType.ToLowerInvariant())
            {
                case "quest":
                    var questRule = rule.ToQuestRule();
                    return questRule.AppliesTo(behaviorAnalysis, contentNeeds);

                case "content":
                    var contentRule = rule.ToContentRule();
                    return contentRule.AppliesTo(behaviorAnalysis, contentNeeds);

                case "narrative":
                    var narrativeRule = rule.ToNarrativeRule();
                    return narrativeRule.AppliesTo(behaviorAnalysis, contentNeeds);

                default:
                    return false;
            }
        }

        private static RuleExecutionResult? ExecuteRule(
            AdaptationRuleDefinition ruleDef,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            NarrativeGenerationConstraints? baseConstraints)
        {
            switch (ruleDef.RuleType.ToLowerInvariant())
            {
                case "quest":
                    // Quest rules are handled by AdaptiveQuestGenerator
                    return new RuleExecutionResult
                    {
                        RuleId = ruleDef.RuleId,
                        Success = true,
                        RuleType = "quest"
                    };

                case "content":
                    // Content rules are handled by DynamicContentInjector
                    return new RuleExecutionResult
                    {
                        RuleId = ruleDef.RuleId,
                        Success = true,
                        RuleType = "content"
                    };

                case "narrative":
                    // Narrative rules modify constraints
                    if (baseConstraints != null)
                    {
                        var narrativeRule = ruleDef.ToNarrativeRule();
                        var adaptedConstraints = narrativeRule.Apply(baseConstraints, behaviorAnalysis, contentNeeds);
                        return new RuleExecutionResult
                        {
                            RuleId = ruleDef.RuleId,
                            Success = true,
                            RuleType = "narrative",
                            AdaptedConstraints = adaptedConstraints
                        };
                    }
                    return null;

                default:
                    return new RuleExecutionResult
                    {
                        RuleId = ruleDef.RuleId,
                        Success = false,
                        ErrorMessage = $"Unknown rule type: {ruleDef.RuleType}"
                    };
            }
        }

        /// <summary>
        /// Gets all loaded rules.
        /// </summary>
        public static List<AdaptationRuleDefinition> GetAllRules()
        {
            lock (_lock)
            {
                return new List<AdaptationRuleDefinition>(_rules);
            }
        }

        /// <summary>
        /// Clears all loaded rules.
        /// </summary>
        public static void ClearRules()
        {
            lock (_lock)
            {
                _rules.Clear();
            }
        }
    }

    /// <summary>
    /// Result of executing an adaptation rule.
    /// </summary>
    public sealed class RuleExecutionResult
    {
        public string RuleId { get; set; } = string.Empty;
        public string RuleType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public NarrativeGenerationConstraints? AdaptedConstraints { get; set; }
    }
}

