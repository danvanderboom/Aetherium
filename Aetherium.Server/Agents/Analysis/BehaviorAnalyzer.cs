using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;

using Aetherium.Model.Analysis;
namespace Aetherium.Server.Agents.Analysis
{
    /// <summary>
    /// Analyzes agent behavior patterns from telemetry data.
    /// </summary>
    public static class BehaviorAnalyzer
    {
        /// <summary>
        /// Analyzes behavior patterns from performance snapshots.
        /// </summary>
        public static BehaviorAnalysis AnalyzeBehavior(List<PerformanceSnapshot> snapshots, List<ReplayData>? replays = null)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return new BehaviorAnalysis
                {
                    AgentId = snapshots?.FirstOrDefault()?.AgentId ?? string.Empty
                };
            }

            var agentId = snapshots[0].AgentId;
            var analysis = new BehaviorAnalysis
            {
                AgentId = agentId,
                AnalysisTimestamp = DateTime.UtcNow,
                TotalSteps = snapshots.Count
            };

            // Analyze action preferences
            AnalyzeActionPreferences(snapshots, analysis);

            // Analyze exploration patterns (if location data available)
            AnalyzeExplorationPatterns(snapshots, analysis);

            // Analyze struggle patterns
            AnalyzeStrugglePatterns(snapshots, analysis);

            // Analyze success patterns
            AnalyzeSuccessPatterns(snapshots, analysis);

            // Analyze replay data if available
            if (replays != null && replays.Count > 0)
            {
                AnalyzeFailureContexts(replays, analysis);
            }

            // Calculate average perception complexity
            var complexities = snapshots.Where(s => s.PerceptionComplexity > 0).Select(s => s.PerceptionComplexity).ToList();
            analysis.AveragePerceptionComplexity = complexities.Any() ? complexities.Average() : 0;

            // Extract interaction patterns from action types and summaries
            AnalyzeInteractionPatterns(snapshots, analysis);

            return analysis;
        }

        /// <summary>
        /// Builds an interest profile from behavior analysis.
        /// </summary>
        public static InterestProfile BuildInterestProfile(BehaviorAnalysis behaviorAnalysis)
        {
            var profile = new InterestProfile
            {
                AgentId = behaviorAnalysis.AgentId,
                LastUpdated = DateTime.UtcNow
            };

            // Build action preferences
            foreach (var actionPattern in behaviorAnalysis.ActionPatterns)
            {
                var preference = new ActionPreference
                {
                    ActionType = actionPattern.ActionType,
                    UsageCount = actionPattern.TotalCount,
                    SuccessCount = actionPattern.SuccessCount,
                    SuccessRate = actionPattern.SuccessRate,
                    AverageLatencyMs = actionPattern.AverageLatencyMs,
                    LastUsed = actionPattern.LastUsed,
                    PreferenceScore = CalculateActionPreferenceScore(actionPattern)
                };

                profile.ActionPreferences[actionPattern.ActionType] = preference;
            }

            // Build entity interests from interaction patterns
            foreach (var interaction in behaviorAnalysis.InteractionPatterns)
            {
                if (profile.EntityInterests.ContainsKey(interaction.EntityType))
                {
                    profile.EntityInterests[interaction.EntityType] += interaction.InteractionCount;
                }
                else
                {
                    profile.EntityInterests[interaction.EntityType] = interaction.InteractionCount;
                }
            }

            // Build exploration patterns
            foreach (var areaPattern in behaviorAnalysis.ExplorationPatterns)
            {
                var areaPreference = new AreaPreference
                {
                    AreaPattern = areaPattern.AreaType,
                    VisitCount = areaPattern.VisitCount,
                    AverageTimeSpent = areaPattern.AverageTimeSpent,
                    SuccessRate = areaPattern.SuccessRate,
                    IsAvoided = areaPattern.VisitCount < 2, // Rarely visited = avoided
                    PreferenceScore = CalculateAreaPreferenceScore(areaPattern)
                };

                profile.ExplorationPatterns[areaPattern.AreaType] = areaPreference;
            }

            // Identify most engaging interactions
            var engagingInteractions = behaviorAnalysis.InteractionPatterns
                .OrderByDescending(i => i.InteractionCount)
                .Take(5)
                .Select(i => i.EntityType)
                .ToList();
            profile.EngagingInteractions = engagingInteractions;

            // Identify preferred content types
            var preferredContent = behaviorAnalysis.SuccessPatterns
                .Where(s => s.SuccessRate > 0.7)
                .Select(s => s.ContextType)
                .Distinct()
                .ToList();
            profile.PreferredContentTypes = preferredContent;

            return profile;
        }

        /// <summary>
        /// Maps behavior weaknesses to contextual content needs.
        /// </summary>
        public static List<ContentNeed> MapWeaknessesToContentNeeds(BehaviorAnalysis behaviorAnalysis)
        {
            var needs = new List<ContentNeed>();

            // Navigation struggles
            var navigationFailures = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("navigation") || s.ContextType.Contains("movement"))
                .ToList();
            if (navigationFailures.Count > 0)
            {
                needs.Add(new ContentNeed
                {
                    NeedType = "navigation_assistance",
                    Priority = CalculatePriority(navigationFailures),
                    SuggestedContent = new List<string> { "simpler_layout", "navigation_hints", "better_waypoints" },
                    Description = "Agent struggles with navigation - suggest simpler layouts or hints"
                });
            }

            // Key-lock struggles
            var lockFailures = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("key") || s.ContextType.Contains("lock") || s.ContextType.Contains("door"))
                .ToList();
            if (lockFailures.Count > 0)
            {
                needs.Add(new ContentNeed
                {
                    NeedType = "key_lock_assistance",
                    Priority = CalculatePriority(lockFailures),
                    SuggestedContent = new List<string> { "fewer_locks", "better_key_placement", "lock_hints" },
                    Description = "Agent struggles with key-lock puzzles - suggest fewer locks or better placement"
                });
            }

            // Combat struggles
            var combatFailures = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("combat") || s.ContextType.Contains("enemy") || s.ContextType.Contains("monster"))
                .ToList();
            if (combatFailures.Count > 0)
            {
                needs.Add(new ContentNeed
                {
                    NeedType = "combat_assistance",
                    Priority = CalculatePriority(combatFailures),
                    SuggestedContent = new List<string> { "fewer_enemies", "combat_tools", "combat_hints" },
                    Description = "Agent struggles with combat - suggest fewer enemies or combat assistance"
                });
            }

            // Puzzle struggles
            var puzzleFailures = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains("puzzle") || s.ContextType.Contains("interaction"))
                .ToList();
            if (puzzleFailures.Count > 0)
            {
                needs.Add(new ContentNeed
                {
                    NeedType = "puzzle_assistance",
                    Priority = CalculatePriority(puzzleFailures),
                    SuggestedContent = new List<string> { "simpler_puzzles", "puzzle_hints", "puzzle_tutorials" },
                    Description = "Agent struggles with puzzles - suggest simpler puzzles or hints"
                });
            }

            // High perception complexity
            if (behaviorAnalysis.AveragePerceptionComplexity > 100)
            {
                needs.Add(new ContentNeed
                {
                    NeedType = "perception_simplification",
                    Priority = 0.7,
                    SuggestedContent = new List<string> { "reduce_entity_density", "smaller_maps", "focused_areas" },
                    Description = "Agent overwhelmed by perception complexity - suggest simpler environments"
                });
            }

            return needs;
        }

        private static void AnalyzeActionPreferences(List<PerformanceSnapshot> snapshots, BehaviorAnalysis analysis)
        {
            var actionGroups = snapshots.GroupBy(s => s.ActionType ?? "unknown");
            foreach (var group in actionGroups)
            {
                var actionSnapshots = group.ToList();
                var pattern = new ActionPattern
                {
                    ActionType = group.Key,
                    TotalCount = actionSnapshots.Count,
                    SuccessCount = actionSnapshots.Count(s => s.ActionSucceeded),
                    FailureCount = actionSnapshots.Count(s => !s.ActionSucceeded),
                    LastUsed = actionSnapshots.Max(s => s.Timestamp)
                };

                pattern.SuccessRate = pattern.TotalCount > 0
                    ? (double)pattern.SuccessCount / pattern.TotalCount
                    : 0.0;

                var latencies = actionSnapshots.Where(s => s.DecisionLatencyMs > 0).Select(s => s.DecisionLatencyMs).ToList();
                pattern.AverageLatencyMs = latencies.Any() ? latencies.Average() : 0;

                analysis.ActionPatterns.Add(pattern);
            }
        }

        private static void AnalyzeExplorationPatterns(List<PerformanceSnapshot> snapshots, BehaviorAnalysis analysis)
        {
            // Group by session to analyze exploration within sessions
            var sessionGroups = snapshots.GroupBy(s => s.SessionId);
            foreach (var sessionGroup in sessionGroups)
            {
                // Analyze exploration based on step progression
                var sessionSnapshots = sessionGroup.OrderBy(s => s.StepNumber).ToList();
                
                // Simple area categorization based on step progression
                // First 25% = early exploration, last 25% = late exploration
                var totalSteps = sessionSnapshots.Count;
                if (totalSteps > 4)
                {
                    var earlySteps = sessionSnapshots.Take(totalSteps / 4).ToList();
                    var lateSteps = sessionSnapshots.Skip(3 * totalSteps / 4).ToList();

                    var earlyPattern = new AreaPattern
                    {
                        AreaType = "early_exploration",
                        VisitCount = earlySteps.Count,
                        SuccessRate = earlySteps.Count > 0 ? (double)earlySteps.Count(s => s.ActionSucceeded) / earlySteps.Count : 0.0
                    };
                    analysis.ExplorationPatterns.Add(earlyPattern);

                    var latePattern = new AreaPattern
                    {
                        AreaType = "late_exploration",
                        VisitCount = lateSteps.Count,
                        SuccessRate = lateSteps.Count > 0 ? (double)lateSteps.Count(s => s.ActionSucceeded) / lateSteps.Count : 0.0
                    };
                    analysis.ExplorationPatterns.Add(latePattern);
                }
            }
        }

        private static void AnalyzeStrugglePatterns(List<PerformanceSnapshot> snapshots, BehaviorAnalysis analysis)
        {
            // Identify failure sequences
            var failureSequences = new List<List<PerformanceSnapshot>>();
            var currentSequence = new List<PerformanceSnapshot>();

            foreach (var snapshot in snapshots.OrderBy(s => s.Timestamp))
            {
                if (!snapshot.ActionSucceeded)
                {
                    currentSequence.Add(snapshot);
                }
                else
                {
                    if (currentSequence.Count >= 2)
                    {
                        failureSequences.Add(new List<PerformanceSnapshot>(currentSequence));
                    }
                    currentSequence.Clear();
                }
            }

            if (currentSequence.Count >= 2)
            {
                failureSequences.Add(currentSequence);
            }

            // Analyze struggle patterns
            foreach (var sequence in failureSequences)
            {
                var strugglePattern = new StrugglePattern
                {
                    ContextType = InferContextType(sequence),
                    FailureCount = sequence.Count,
                    FirstFailureStep = sequence.Min(s => s.StepNumber),
                    LastFailureStep = sequence.Max(s => s.StepNumber),
                    CommonActionTypes = sequence.GroupBy(s => s.ActionType)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList()
                };

                analysis.StrugglePatterns.Add(strugglePattern);
            }

            // If no sequences detected, consider single failures as indicative patterns
            if (failureSequences.Count == 0)
            {
                var failed = snapshots.Where(s => !s.ActionSucceeded).OrderBy(s => s.Timestamp).ToList();
                if (failed.Count > 0)
                {
                    var strugglePattern = new StrugglePattern
                    {
                        ContextType = InferContextType(failed.Take(1).ToList()),
                        FailureCount = 1,
                        FirstFailureStep = failed.First().StepNumber,
                        LastFailureStep = failed.First().StepNumber,
                        CommonActionTypes = new List<string> { failed.First().ActionType ?? "unknown" }
                    };

                    analysis.StrugglePatterns.Add(strugglePattern);
                }
            }
        }

        private static void AnalyzeInteractionPatterns(List<PerformanceSnapshot> snapshots, BehaviorAnalysis analysis)
        {
            // Extract entity types from action summaries (e.g., "pickup key-123" -> entity type "key")
            var interactionGroups = snapshots
                .Where(s => !string.IsNullOrEmpty(s.ActionSummary))
                .GroupBy(s =>
                {
                    var summary = s.ActionSummary.ToLower();
                    if (summary.Contains("key")) return "key";
                    if (summary.Contains("door")) return "door";
                    if (summary.Contains("chest")) return "chest";
                    if (summary.Contains("monster") || summary.Contains("enemy")) return "monster";
                    if (summary.Contains("npc")) return "npc";
                    if (summary.Contains("item")) return "item";
                    return "unknown";
                })
                .Where(g => g.Key != "unknown");

            foreach (var group in interactionGroups)
            {
                var interactionSnapshots = group.ToList();
                var pattern = new InteractionPattern
                {
                    EntityType = group.Key,
                    InteractionCount = interactionSnapshots.Count,
                    SuccessRate = interactionSnapshots.Count > 0
                        ? (double)interactionSnapshots.Count(s => s.ActionSucceeded) / interactionSnapshots.Count
                        : 0.0
                };

                analysis.InteractionPatterns.Add(pattern);
            }
        }

        private static void AnalyzeSuccessPatterns(List<PerformanceSnapshot> snapshots, BehaviorAnalysis analysis)
        {
            // Identify successful action sequences
            var successGroups = snapshots
                .Where(s => s.ActionSucceeded)
                .GroupBy(s => s.ActionType)
                .ToList();

            foreach (var group in successGroups)
            {
                var successSnapshots = group.ToList();
                if (successSnapshots.Count >= 3)
                {
                    var successPattern = new SuccessPattern
                    {
                        ContextType = group.Key + "_success",
                        SuccessCount = successSnapshots.Count,
                        SuccessRate = 1.0, // All successful
                        AverageLatencyMs = successSnapshots.Where(s => s.DecisionLatencyMs > 0)
                            .Select(s => s.DecisionLatencyMs)
                            .DefaultIfEmpty(0)
                            .Average()
                    };

                    analysis.SuccessPatterns.Add(successPattern);
                }
            }
        }

        private static void AnalyzeFailureContexts(List<ReplayData> replays, BehaviorAnalysis analysis)
        {
            foreach (var replay in replays)
            {
                var failurePattern = new StrugglePattern
                {
                    ContextType = InferContextTypeFromReplay(replay),
                    FailureCount = replay.Steps.Count(s => !s.Succeeded),
                    FirstFailureStep = replay.Steps.Where(s => !s.Succeeded).Min(s => s.StepNumber),
                    LastFailureStep = replay.Steps.Where(s => !s.Succeeded).Max(s => s.StepNumber),
                    CommonActionTypes = replay.Steps
                        .Where(s => !s.Succeeded)
                        .GroupBy(s => s.ActionType)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => g.Key)
                        .ToList(),
                    FailureReason = replay.FailureReason
                };

                analysis.StrugglePatterns.Add(failurePattern);
            }
        }

        private static string InferContextType(List<PerformanceSnapshot> sequence)
        {
            // Infer context from action types and error messages
            var actionTypes = sequence.Select(s => s.ActionType).Distinct().ToList();
            var errorMessages = sequence.Where(s => !string.IsNullOrEmpty(s.ErrorMessage)).Select(s => s.ErrorMessage!.ToLower()).ToList();

            if (actionTypes.Contains("move") || actionTypes.Contains("navigation"))
            {
                return "navigation_failure";
            }
            if (actionTypes.Contains("pickup") && errorMessages.Any(e => e.Contains("key") || e.Contains("lock")))
            {
                return "key_lock_failure";
            }
            if (errorMessages.Any(e => e.Contains("combat") || e.Contains("enemy") || e.Contains("monster")))
            {
                return "combat_failure";
            }
            if (errorMessages.Any(e => e.Contains("puzzle") || e.Contains("interaction")))
            {
                return "puzzle_failure";
            }

            return "general_failure";
        }

        private static string InferContextTypeFromReplay(ReplayData replay)
        {
            var failedSteps = replay.Steps.Where(s => !s.Succeeded).ToList();
            var actionTypes = failedSteps.Select(s => s.ActionType).Distinct().ToList();

            if (replay.FailureReason.Contains("navigation", StringComparison.OrdinalIgnoreCase) ||
                actionTypes.Contains("move"))
            {
                return "navigation_failure";
            }
            if (replay.FailureReason.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                replay.FailureReason.Contains("lock", StringComparison.OrdinalIgnoreCase))
            {
                return "key_lock_failure";
            }
            if (replay.FailureReason.Contains("combat", StringComparison.OrdinalIgnoreCase) ||
                replay.FailureReason.Contains("enemy", StringComparison.OrdinalIgnoreCase))
            {
                return "combat_failure";
            }

            return "general_failure";
        }

        private static double CalculateActionPreferenceScore(ActionPattern pattern)
        {
            // Higher usage + higher success rate = higher preference
            var usageScore = Math.Min(1.0, pattern.TotalCount / 20.0); // Max at 20 uses
            var successScore = pattern.SuccessRate;
            var latencyScore = pattern.AverageLatencyMs > 0 
                ? Math.Max(0, 1.0 - (pattern.AverageLatencyMs / 5000.0)) // Prefer lower latency
                : 0.5;

            return (usageScore * 0.4 + successScore * 0.4 + latencyScore * 0.2);
        }

        private static double CalculateAreaPreferenceScore(AreaPattern pattern)
        {
            // Higher visit count + higher success rate = higher preference
            var visitScore = Math.Min(1.0, pattern.VisitCount / 10.0);
            var successScore = pattern.SuccessRate;

            return (visitScore * 0.5 + successScore * 0.5);
        }

        private static double CalculatePriority(List<StrugglePattern> patterns)
        {
            // Priority based on frequency and severity
            var totalFailures = patterns.Sum(p => p.FailureCount);
            var avgFailureRate = patterns.Average(p => p.FailureCount);

            return Math.Min(1.0, (totalFailures / 10.0) * 0.6 + (avgFailureRate / 5.0) * 0.4);
        }
    }

    // BehaviorAnalysis and its pattern DTOs (ActionPattern, AreaPattern, StrugglePattern,
    // SuccessPattern, InteractionPattern, ContentNeed) now live in Aetherium.Model so clients can
    // consume them without referencing Aetherium.Server. This file keeps only the producing logic
    // (BehaviorAnalyzer). See openspec/changes/move-contracts-to-model.
}

