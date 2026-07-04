using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Analysis
{
    // NOTE: These shared behavior-analysis DTOs live in Aetherium.Model so
    // clients such as Aetherium.Dashboard can deserialize them without referencing Aetherium.Server.
    // The producing logic (BehaviorAnalyzer) stays in Aetherium.Server. See
    // openspec/changes/move-contracts-to-model.

    /// <summary>
    /// Comprehensive behavior analysis result.
    /// </summary>
    [GenerateSerializer]
    public sealed class BehaviorAnalysis
    {
        [Id(0)]
        public string AgentId { get; set; } = string.Empty;
        [Id(1)]
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
        [Id(2)]
        public int TotalSteps { get; set; }

        [Id(3)]
        public List<ActionPattern> ActionPatterns { get; set; } = new List<ActionPattern>();
        [Id(4)]
        public List<AreaPattern> ExplorationPatterns { get; set; } = new List<AreaPattern>();
        [Id(5)]
        public List<StrugglePattern> StrugglePatterns { get; set; } = new List<StrugglePattern>();
        [Id(6)]
        public List<SuccessPattern> SuccessPatterns { get; set; } = new List<SuccessPattern>();
        [Id(7)]
        public List<InteractionPattern> InteractionPatterns { get; set; } = new List<InteractionPattern>();

        [Id(8)]
        public double AveragePerceptionComplexity { get; set; }
    }

    [GenerateSerializer]
    public sealed class ActionPattern
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
        [Id(6)]
        public DateTime LastUsed { get; set; }
    }

    [GenerateSerializer]
    public sealed class AreaPattern
    {
        [Id(0)]
        public string AreaType { get; set; } = string.Empty;
        [Id(1)]
        public int VisitCount { get; set; }
        [Id(2)]
        public double AverageTimeSpent { get; set; }
        [Id(3)]
        public double SuccessRate { get; set; }
    }

    [GenerateSerializer]
    public sealed class StrugglePattern
    {
        [Id(0)]
        public string ContextType { get; set; } = string.Empty;
        [Id(1)]
        public int FailureCount { get; set; }
        [Id(2)]
        public int FirstFailureStep { get; set; }
        [Id(3)]
        public int LastFailureStep { get; set; }
        [Id(4)]
        public List<string> CommonActionTypes { get; set; } = new List<string>();
        [Id(5)]
        public string? FailureReason { get; set; }
    }

    [GenerateSerializer]
    public sealed class SuccessPattern
    {
        [Id(0)]
        public string ContextType { get; set; } = string.Empty;
        [Id(1)]
        public int SuccessCount { get; set; }
        [Id(2)]
        public double SuccessRate { get; set; }
        [Id(3)]
        public double AverageLatencyMs { get; set; }
    }

    [GenerateSerializer]
    public sealed class InteractionPattern
    {
        [Id(0)]
        public string EntityType { get; set; } = string.Empty;
        [Id(1)]
        public int InteractionCount { get; set; }
        [Id(2)]
        public double SuccessRate { get; set; }
    }

    [GenerateSerializer]
    public sealed class ContentNeed
    {
        [Id(0)]
        public string NeedType { get; set; } = string.Empty;
        [Id(1)]
        public double Priority { get; set; } // 0.0 to 1.0
        [Id(2)]
        public List<string> SuggestedContent { get; set; } = new List<string>();
        [Id(3)]
        public string Description { get; set; } = string.Empty;
    }
}
