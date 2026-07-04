using System;
using System.Collections.Generic;

namespace Aetherium.Model.Telemetry
{
    // NOTE: ReplayData/ReplayStep are shared contracts (a SignalR "ReplayStored" payload the
    // dashboard receives), so they live in Aetherium.Model. The former
    // InitialWorldState (Aetherium.Core.World) field was removed: it was always null, never read,
    // and its engine coupling would have forced Model to reference the whole Core engine.
    // The ReplayStorage logic stays in Aetherium.Server. See openspec/changes/move-contracts-to-model.

    /// <summary>
    /// Action sequence for a failed agent run, used for replay analysis.
    /// </summary>
    public sealed class ReplayData
    {
        public string ReplayId { get; set; } = Guid.NewGuid().ToString();
        public string AgentId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string BenchmarkName { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public int TotalSteps { get; set; }

        public List<ReplayStep> Steps { get; set; } = new List<ReplayStep>();

        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A single step in a replay sequence.
    /// </summary>
    public sealed class ReplayStep
    {
        public int StepNumber { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string ActionSummary { get; set; } = string.Empty;

        public Dictionary<string, object> ActionArgs { get; set; } = new Dictionary<string, object>();
        public bool Succeeded { get; set; }
        public string? PerceptionJson { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
