using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Telemetry
{
    /// <summary>
    /// Timestamped performance metrics captured at each agent step.
    /// </summary>
    [GenerateSerializer]
    public sealed class PerformanceSnapshot
    {
        [Id(0)]
        public DateTime Timestamp { get; set; }

        [Id(1)]
        public int StepNumber { get; set; }

        [Id(2)]
        public string AgentId { get; set; } = string.Empty;

        [Id(3)]
        public string SessionId { get; set; } = string.Empty;

        [Id(4)]
        public string ActionType { get; set; } = string.Empty;

        [Id(5)]
        public string ActionSummary { get; set; } = string.Empty;

        [Id(6)]
        public bool ActionSucceeded { get; set; }

        [Id(7)]
        public string? ErrorMessage { get; set; }

        [Id(8)]
        public long DecisionLatencyMs { get; set; }

        [Id(9)]
        public int PerceptionComplexity { get; set; }

        [Id(10)]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}

