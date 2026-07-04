using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;
using Orleans;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for accessing agent telemetry data.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AgentTelemetryController : ControllerBase
    {
        private readonly IClusterClient _orleansClient;

        public AgentTelemetryController(IClusterClient orleansClient)
        {
            _orleansClient = orleansClient;
        }

        /// <summary>
        /// Gets performance analysis for an agent.
        /// </summary>
        [HttpGet("{agentId}/analysis")]
        public async Task<ActionResult<PerformanceAnalysis>> GetAnalysis(string agentId)
        {
            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            var analysis = await telemetryGrain.GetAnalysisAsync();
            
            if (analysis == null)
                return NotFound($"No telemetry data found for agent: {agentId}");

            return Ok(analysis);
        }

        /// <summary>
        /// Gets performance snapshots for an agent.
        /// </summary>
        [HttpGet("{agentId}/snapshots")]
        public async Task<ActionResult<List<PerformanceSnapshot>>> GetSnapshots(string agentId, [FromQuery] int? limit = null)
        {
            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            var snapshots = await telemetryGrain.GetSnapshotsAsync(limit);
            
            return Ok(snapshots);
        }

        /// <summary>
        /// Gets snapshots within a time range.
        /// </summary>
        [HttpGet("{agentId}/snapshots/range")]
        public async Task<ActionResult<List<PerformanceSnapshot>>> GetSnapshotsInRange(
            string agentId,
            [FromQuery] DateTime startTime,
            [FromQuery] DateTime endTime)
        {
            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            var snapshots = await telemetryGrain.GetSnapshotsInRangeAsync(startTime, endTime);
            
            return Ok(snapshots);
        }

        /// <summary>
        /// Gets failed run IDs for an agent.
        /// </summary>
        [HttpGet("{agentId}/failed-runs")]
        public async Task<ActionResult<List<string>>> GetFailedRuns(string agentId, [FromQuery] int? limit = null)
        {
            var telemetryGrain = _orleansClient.GetGrain<IAgentTelemetryGrain>(agentId);
            var replayIds = await telemetryGrain.GetFailedRunIdsAsync(limit);
            
            return Ok(replayIds);
        }
    }
}

