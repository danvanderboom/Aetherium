using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.Agents.Telemetry
{
    // NOTE: This shared contract lives in Aetherium.Model (namespace retained) so clients such as
    // Aetherium.Dashboard can consume it without referencing Aetherium.Server. The producing logic
    // (PerformanceAnalyzer) stays in Aetherium.Server. See openspec/changes/move-contracts-to-model.

    /// <summary>
    /// Aggregated performance analysis for an agent, produced from its telemetry snapshots.
    /// </summary>
    [GenerateSerializer]
    public sealed class PerformanceAnalysis
    {
        [Id(0)]
        public string AgentId { get; set; } = string.Empty;

        [Id(1)]
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;

        [Id(2)]
        public int TotalSteps { get; set; }

        [Id(3)]
        public int TotalSuccessfulActions { get; set; }

        [Id(4)]
        public int TotalFailedActions { get; set; }

        [Id(5)]
        public double SuccessRate { get; set; }

        [Id(6)]
        public double AverageDecisionLatencyMs { get; set; }

        [Id(7)]
        public double AveragePerceptionComplexity { get; set; }

        [Id(8)]
        public Dictionary<string, ActionTypeStats> ActionTypeStats { get; set; } = new Dictionary<string, ActionTypeStats>();

        [Id(9)]
        public List<string> IdentifiedWeaknesses { get; set; } = new List<string>();

        [Id(10)]
        public List<string> Recommendations { get; set; } = new List<string>();

        [Id(11)]
        public Dictionary<string, double> TrendMetrics { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Statistics for a specific action type.
    /// </summary>
    [GenerateSerializer]
    public sealed class ActionTypeStats
    {
        [Id(0)]
        public string ActionType { get; set; } = string.Empty;

        [Id(1)]
        public int TotalCount { get; set; }

        [Id(2)]
        public int SuccessCount { get; set; }

        [Id(3)]
        public int FailureCount { get; set; }

        [Id(4)]
        public double SuccessRate { get; set; }

        [Id(5)]
        public double AverageLatencyMs { get; set; }
    }
}
