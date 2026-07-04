using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;
using Orleans;

namespace Aetherium.Server.Agents
{
    /// <summary>
    /// Orchestrates an autonomous agent attached to a game session.
    /// </summary>
    public interface IAgentRunnerGrain : IGrainWithStringKey
    {
        Task<bool> AttachAsync(string sessionId, string agentId);

        /// <summary>
        /// Attaches the agent as a first-class participant in a live, grain-hosted
        /// map — it joins as a Character and acts through the map grain, so its
        /// actions fan out to human players. No pre-existing human session required.
        /// </summary>
        Task<bool> AttachToWorldAsync(string worldId, string mapId, string agentId);

        Task DetachAsync();
        Task<RunnerStatus> GetStatusAsync();
        Task StepAsync();
        Task RunAsync(int? maxSteps = null, int stepDelayMs = 200);
        Task StopAsync();
        Task<PerformanceAnalysis?> GetTelemetryAsync();
    }

    [GenerateSerializer]
    public class RunnerStatus
    {
        [Id(0)]
        public string? SessionId { get; set; }
        [Id(1)]
        public string? AgentId { get; set; }
        [Id(2)]
        public bool IsRunning { get; set; }
        [Id(3)]
        public int Steps { get; set; }
        [Id(4)]
        public string LastAction { get; set; } = string.Empty;
        [Id(5)]
        public string LastResult { get; set; } = string.Empty;
    }
}



