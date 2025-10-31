using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Orleans;

namespace Aetherium.Server.Agents.Telemetry
{
    /// <summary>
    /// Orleans grain implementation for storing agent performance telemetry.
    /// </summary>
    public class AgentTelemetryGrain : Grain, IAgentTelemetryGrain
    {
        private readonly List<PerformanceSnapshot> _snapshots = new List<PerformanceSnapshot>();
        private readonly List<string> _failedRunIds = new List<string>();
        private PerformanceAnalysis? _cachedAnalysis;
        private DateTime _lastAnalysisTime = DateTime.MinValue;
        private static readonly TimeSpan AnalysisCacheTimeout = TimeSpan.FromMinutes(1);

        public override Task OnActivateAsync(System.Threading.CancellationToken cancellationToken)
        {
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AgentTelemetryGrain] Activated for agent: {agentId}");
            return base.OnActivateAsync(cancellationToken);
        }

        public Task RecordSnapshotAsync(PerformanceSnapshot snapshot)
        {
            if (snapshot == null)
                return Task.CompletedTask;

            lock (_snapshots)
            {
                _snapshots.Add(snapshot);
                
                // Invalidate cached analysis if it's stale
                if (DateTime.UtcNow - _lastAnalysisTime > AnalysisCacheTimeout)
                {
                    _cachedAnalysis = null;
                }
            }

            return Task.CompletedTask;
        }

        public Task<List<PerformanceSnapshot>> GetSnapshotsAsync(int? limit = null)
        {
            lock (_snapshots)
            {
                var results = new List<PerformanceSnapshot>(_snapshots);
                
                if (limit.HasValue && results.Count > limit.Value)
                {
                    results = results.Skip(Math.Max(0, results.Count - limit.Value)).ToList();
                }

                return Task.FromResult(results);
            }
        }

        public Task<List<PerformanceSnapshot>> GetSnapshotsInRangeAsync(DateTime startTime, DateTime endTime)
        {
            lock (_snapshots)
            {
                var results = _snapshots
                    .Where(s => s.Timestamp >= startTime && s.Timestamp <= endTime)
                    .OrderBy(s => s.Timestamp)
                    .ToList();

                return Task.FromResult(results);
            }
        }

        public Task<PerformanceAnalysis> GetAnalysisAsync()
        {
            lock (_snapshots)
            {
                // Return cached analysis if still valid
                if (_cachedAnalysis != null && DateTime.UtcNow - _lastAnalysisTime < AnalysisCacheTimeout)
                {
                    return Task.FromResult(_cachedAnalysis);
                }

                // Recompute analysis
                var agentId = this.GetPrimaryKeyString();
                _cachedAnalysis = PerformanceAnalyzer.Analyze(_snapshots, agentId);
                _lastAnalysisTime = DateTime.UtcNow;

                return Task.FromResult(_cachedAnalysis);
            }
        }

        public Task<string> RecordFailedRunAsync(ReplayData replayData)
        {
            if (replayData == null)
                return Task.FromResult(string.Empty);

            var replayId = ReplayStorage.StoreReplay(replayData);
            
            lock (_failedRunIds)
            {
                _failedRunIds.Add(replayId);
                // Keep only last 100 replay IDs in memory
                if (_failedRunIds.Count > 100)
                {
                    _failedRunIds.RemoveRange(0, _failedRunIds.Count - 100);
                }
            }

            return Task.FromResult(replayId);
        }

        public Task<List<string>> GetFailedRunIdsAsync(int? limit = null)
        {
            lock (_failedRunIds)
            {
                var results = new List<string>(_failedRunIds);
                
                if (limit.HasValue && results.Count > limit.Value)
                {
                    results = results.Skip(Math.Max(0, results.Count - limit.Value)).ToList();
                }

                return Task.FromResult(results);
            }
        }

        public Task ClearTelemetryAsync()
        {
            lock (_snapshots)
            lock (_failedRunIds)
            {
                _snapshots.Clear();
                _failedRunIds.Clear();
                _cachedAnalysis = null;
            }

            return Task.CompletedTask;
        }
    }
}

