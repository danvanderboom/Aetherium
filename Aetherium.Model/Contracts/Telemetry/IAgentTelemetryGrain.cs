using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Aetherium.Model.Telemetry
{
    /// <summary>
    /// Orleans grain for storing and retrieving agent performance telemetry.
    /// </summary>
    public interface IAgentTelemetryGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Records a performance snapshot for the agent.
        /// </summary>
        Task RecordSnapshotAsync(PerformanceSnapshot snapshot);

        /// <summary>
        /// Gets all performance snapshots for this agent.
        /// </summary>
        Task<List<PerformanceSnapshot>> GetSnapshotsAsync(int? limit = null);

        /// <summary>
        /// Gets snapshots within a time range.
        /// </summary>
        Task<List<PerformanceSnapshot>> GetSnapshotsInRangeAsync(System.DateTime startTime, System.DateTime endTime);

        /// <summary>
        /// Gets the latest performance analysis for this agent.
        /// </summary>
        Task<PerformanceAnalysis> GetAnalysisAsync();

        /// <summary>
        /// Records a failed run for replay storage (serialized JSON payload).
        /// </summary>
        Task<string> RecordFailedRunAsync(string replayJson);

        /// <summary>
        /// Gets replay IDs for failed runs.
        /// </summary>
        Task<List<string>> GetFailedRunIdsAsync(int? limit = null);

        /// <summary>
        /// Clears all telemetry data for this agent.
        /// </summary>
        Task ClearTelemetryAsync();

        /// <summary>
        /// Gets a serialized replay (JSON) by ID.
        /// </summary>
        Task<string?> GetReplayAsync(string replayId);
    }
}

