using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;

namespace Aetherium.Server.Agents.Telemetry
{
    // PerformanceAnalysis and ActionTypeStats (the shared DTOs) now live in Aetherium.Model so
    // clients can consume them without referencing Aetherium.Server. This file keeps only the
    // producing logic. See openspec/changes/move-contracts-to-model.

    /// <summary>
    /// Analyzes performance snapshots and produces insights.
    /// </summary>
    public static class PerformanceAnalyzer
    {
        /// <summary>
        /// Analyzes a collection of performance snapshots and produces an analysis.
        /// </summary>
        public static PerformanceAnalysis Analyze(List<PerformanceSnapshot> snapshots, string agentId)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return new PerformanceAnalysis
                {
                    AgentId = agentId,
                    TotalSteps = 0
                };
            }

            var analysis = new PerformanceAnalysis
            {
                AgentId = agentId,
                TotalSteps = snapshots.Count,
                TotalSuccessfulActions = snapshots.Count(s => s.ActionSucceeded),
                TotalFailedActions = snapshots.Count(s => !s.ActionSucceeded)
            };

            analysis.SuccessRate = analysis.TotalSteps > 0
                ? (double)analysis.TotalSuccessfulActions / analysis.TotalSteps
                : 0.0;

            var latencies = snapshots.Where(s => s.DecisionLatencyMs > 0).Select(s => s.DecisionLatencyMs).ToList();
            analysis.AverageDecisionLatencyMs = latencies.Any() ? latencies.Average() : 0;

            var complexities = snapshots.Where(s => s.PerceptionComplexity > 0).Select(s => s.PerceptionComplexity).ToList();
            analysis.AveragePerceptionComplexity = complexities.Any() ? complexities.Average() : 0;

            // Group by action type
            var actionGroups = snapshots.GroupBy(s => s.ActionType ?? "unknown");
            foreach (var group in actionGroups)
            {
                var actionType = group.Key;
                var actionSnapshots = group.ToList();
                var stats = new ActionTypeStats
                {
                    ActionType = actionType,
                    TotalCount = actionSnapshots.Count,
                    SuccessCount = actionSnapshots.Count(s => s.ActionSucceeded),
                    FailureCount = actionSnapshots.Count(s => !s.ActionSucceeded)
                };

                stats.SuccessRate = stats.TotalCount > 0
                    ? (double)stats.SuccessCount / stats.TotalCount
                    : 0.0;

                var actionLatencies = actionSnapshots.Where(s => s.DecisionLatencyMs > 0).Select(s => s.DecisionLatencyMs).ToList();
                stats.AverageLatencyMs = actionLatencies.Any() ? actionLatencies.Average() : 0;

                analysis.ActionTypeStats[actionType] = stats;
            }

            // Identify weaknesses
            IdentifyWeaknesses(analysis);

            // Calculate trends (comparing first half to second half)
            if (snapshots.Count >= 10)
            {
                CalculateTrends(snapshots, analysis);
            }

            // Generate recommendations (after trends are computed)
            GenerateRecommendations(analysis);

            return analysis;
        }

        private static void IdentifyWeaknesses(PerformanceAnalysis analysis)
        {
            // Low success rate overall
            if (analysis.SuccessRate < 0.5 && analysis.TotalSteps > 10)
            {
                analysis.IdentifiedWeaknesses.Add($"Low overall success rate ({analysis.SuccessRate:P1})");
            }

            // High failure rate for specific action types
            foreach (var stats in analysis.ActionTypeStats.Values)
            {
                if (stats.TotalCount >= 5 && stats.SuccessRate < 0.3)
                {
                    analysis.IdentifiedWeaknesses.Add($"High failure rate for '{stats.ActionType}' actions ({stats.SuccessRate:P1})");
                }
            }

            // High decision latency
            if (analysis.AverageDecisionLatencyMs > 2000)
            {
                analysis.IdentifiedWeaknesses.Add($"High decision latency ({analysis.AverageDecisionLatencyMs:F0}ms average)");
            }

            // High perception complexity issues
            if (analysis.AveragePerceptionComplexity > 100)
            {
                analysis.IdentifiedWeaknesses.Add($"High perception complexity ({analysis.AveragePerceptionComplexity:F0} entities) may be overwhelming");
            }
        }

        private static void GenerateRecommendations(PerformanceAnalysis analysis)
        {
            // Recommendations based on weaknesses
            if (analysis.SuccessRate < 0.5)
            {
                analysis.Recommendations.Add("Consider reducing difficulty or providing simpler training scenarios");
            }

            foreach (var weakness in analysis.IdentifiedWeaknesses.Where(w => w.Contains("failure rate")))
            {
                var actionType = weakness.Split('\'')[1];
                analysis.Recommendations.Add($"Focus training on improving '{actionType}' action success rate");
            }

            if (analysis.AverageDecisionLatencyMs > 2000)
            {
                analysis.Recommendations.Add($"High decision latency detected ({analysis.AverageDecisionLatencyMs:F0}ms avg) - optimize or reduce complexity");
            }

            if (analysis.TrendMetrics.ContainsKey("success_rate_trend") && analysis.TrendMetrics["success_rate_trend"] < 0)
            {
                analysis.Recommendations.Add("Performance is degrading over time - consider resetting or adjusting training parameters");
            }
        }

        private static void CalculateTrends(List<PerformanceSnapshot> snapshots, PerformanceAnalysis analysis)
        {
            var midpoint = snapshots.Count / 2;
            var firstHalf = snapshots.Take(midpoint).ToList();
            var secondHalf = snapshots.Skip(midpoint).ToList();

            var firstHalfSuccessRate = firstHalf.Count > 0
                ? (double)firstHalf.Count(s => s.ActionSucceeded) / firstHalf.Count
                : 0.0;

            var secondHalfSuccessRate = secondHalf.Count > 0
                ? (double)secondHalf.Count(s => s.ActionSucceeded) / secondHalf.Count
                : 0.0;

            analysis.TrendMetrics["success_rate_trend"] = secondHalfSuccessRate - firstHalfSuccessRate;

            var firstHalfLatency = firstHalf.Where(s => s.DecisionLatencyMs > 0).Select(s => s.DecisionLatencyMs).ToList();
            var secondHalfLatency = secondHalf.Where(s => s.DecisionLatencyMs > 0).Select(s => s.DecisionLatencyMs).ToList();

            if (firstHalfLatency.Any() && secondHalfLatency.Any())
            {
                var firstAvg = firstHalfLatency.Average();
                var secondAvg = secondHalfLatency.Average();
                analysis.TrendMetrics["latency_trend"] = secondAvg - firstAvg;
            }
        }
    }
}

