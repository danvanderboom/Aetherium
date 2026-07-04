using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;
using Orleans;

using Aetherium.Model.Analysis;
namespace Aetherium.Server.Agents.Analysis
{
    /// <summary>
    /// Orleans grain implementation for analyzing agent behavior patterns.
    /// </summary>
    public class BehaviorAnalysisGrain : Grain, IBehaviorAnalysisGrain
    {
        private BehaviorAnalysis? _cachedAnalysis;
        private InterestProfile? _cachedProfile;
        private List<ContentNeed>? _cachedContentNeeds;
        private DateTime _lastAnalysisTime = DateTime.MinValue;
        private static readonly TimeSpan AnalysisCacheTimeout = TimeSpan.FromMinutes(5);

        public override Task OnActivateAsync(System.Threading.CancellationToken cancellationToken)
        {
            var agentId = this.GetPrimaryKeyString();
            Console.WriteLine($"[BehaviorAnalysisGrain] Activated for agent: {agentId}");
            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<BehaviorAnalysis> AnalyzeBehaviorAsync()
        {
            // Check cache
            if (_cachedAnalysis != null && DateTime.UtcNow - _lastAnalysisTime < AnalysisCacheTimeout)
            {
                return _cachedAnalysis;
            }

            // Get telemetry data
            var agentId = this.GetPrimaryKeyString();
            var telemetryGrain = GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);
            var snapshots = await telemetryGrain.GetSnapshotsAsync(limit: 1000); // Get recent snapshots

            // Get replay data
            var replayIds = await telemetryGrain.GetFailedRunIdsAsync(limit: 50);
            var replays = new List<ReplayData>();
            foreach (var replayId in replayIds)
            {
                var replayJson = await telemetryGrain.GetReplayAsync(replayId);
                if (!string.IsNullOrWhiteSpace(replayJson))
                {
                    try
                    {
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<ReplayData>(replayJson);
                        if (parsed != null)
                            replays.Add(parsed);
                    }
                    catch { }
                }
            }

            // Analyze behavior
            _cachedAnalysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots, replays);
            _lastAnalysisTime = DateTime.UtcNow;

            // Invalidate dependent caches
            _cachedProfile = null;
            _cachedContentNeeds = null;

            return _cachedAnalysis;
        }

        public async Task<InterestProfile> GetInterestProfileAsync()
        {
            // Check cache
            if (_cachedProfile != null && _cachedAnalysis != null)
            {
                return _cachedProfile;
            }

            // Get or analyze behavior
            var analysis = await AnalyzeBehaviorAsync();

            // Build interest profile
            _cachedProfile = BehaviorAnalyzer.BuildInterestProfile(analysis);

            return _cachedProfile;
        }

        public async Task<List<ContentNeed>> GetContentNeedsAsync()
        {
            // Check cache
            if (_cachedContentNeeds != null && _cachedAnalysis != null)
            {
                return _cachedContentNeeds;
            }

            // Get or analyze behavior
            var analysis = await AnalyzeBehaviorAsync();

            // Map weaknesses to content needs
            _cachedContentNeeds = BehaviorAnalyzer.MapWeaknessesToContentNeeds(analysis);

            return _cachedContentNeeds;
        }

        public async Task UpdateAnalysisAsync()
        {
            // Invalidate cache and re-analyze
            _cachedAnalysis = null;
            _cachedProfile = null;
            _cachedContentNeeds = null;
            _lastAnalysisTime = DateTime.MinValue;

            // Trigger re-analysis
            await AnalyzeBehaviorAsync();
        }

        public async Task<Dictionary<string, ActionPreference>> GetActionPreferencesAsync()
        {
            var profile = await GetInterestProfileAsync();
            return profile.ActionPreferences;
        }

        public async Task<Dictionary<string, AreaPreference>> GetExplorationPatternsAsync()
        {
            var profile = await GetInterestProfileAsync();
            return profile.ExplorationPatterns;
        }
    }
}

