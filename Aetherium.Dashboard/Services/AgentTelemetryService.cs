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

            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            return await telemetryGrain.GetAnalysisAsync();
        }

        public async Task<List<PerformanceSnapshot>> GetSnapshotsAsync(string agentId, int? limit = null)
        {
            if (_orleansClient == null)
                return new List<PerformanceSnapshot>();

            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            return await telemetryGrain.GetSnapshotsAsync(limit);
        }

        public async Task<List<string>> GetFailedRunIdsAsync(string agentId, int? limit = null)
        {
            if (_orleansClient == null)
                return new List<string>();

            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            return await telemetryGrain.GetFailedRunIdsAsync(limit);
        }
    }
}

