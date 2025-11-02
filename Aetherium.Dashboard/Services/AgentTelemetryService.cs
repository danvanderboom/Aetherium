using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Orleans;

namespace Aetherium.Dashboard
{
    /// <summary>
    /// Service for accessing agent telemetry data.
    /// </summary>
    public class AgentTelemetryService
    {
        private readonly IClusterClient? _orleansClient;

        public AgentTelemetryService(IClusterClient? orleansClient = null)
        {
            _orleansClient = orleansClient;
        }

        public async Task<PerformanceAnalysis?> GetTelemetryAsync(string agentId)
        {
            if (_orleansClient == null)
                return null;

            try
            {
                var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
                return await telemetryGrain.GetAnalysisAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgentTelemetryService] Error getting telemetry for agent {agentId}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<PerformanceSnapshot>> GetSnapshotsAsync(string agentId, int? limit = null)
        {
            if (_orleansClient == null)
                return new List<PerformanceSnapshot>();

            try
            {
                var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
                return await telemetryGrain.GetSnapshotsAsync(limit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgentTelemetryService] Error getting snapshots for agent {agentId}: {ex.Message}");
                return new List<PerformanceSnapshot>();
            }
        }

        public async Task<List<string>> GetFailedRunIdsAsync(string agentId, int? limit = null)
        {
            if (_orleansClient == null)
                return new List<string>();

            try
            {
                var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
                return await telemetryGrain.GetFailedRunIdsAsync(limit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgentTelemetryService] Error getting failed run IDs for agent {agentId}: {ex.Message}");
                return new List<string>();
            }
        }
    }
}

