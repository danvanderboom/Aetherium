using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Orleans;

namespace Aetherium.Server.Agents.Analysis
{
    /// <summary>
    /// Orleans grain interface for analyzing agent behavior patterns.
    /// </summary>
    public interface IBehaviorAnalysisGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Analyzes agent behavior from telemetry data.
        /// </summary>
        Task<BehaviorAnalysis> AnalyzeBehaviorAsync();

        /// <summary>
        /// Gets the agent's interest profile.
        /// </summary>
        Task<InterestProfile> GetInterestProfileAsync();

        /// <summary>
        /// Gets content needs based on behavior weaknesses.
        /// </summary>
        Task<List<ContentNeed>> GetContentNeedsAsync();

        /// <summary>
        /// Updates behavior analysis from latest telemetry.
        /// </summary>
        Task UpdateAnalysisAsync();

        /// <summary>
        /// Gets action preferences for the agent.
        /// </summary>
        Task<Dictionary<string, ActionPreference>> GetActionPreferencesAsync();

        /// <summary>
        /// Gets exploration patterns for the agent.
        /// </summary>
        Task<Dictionary<string, AreaPreference>> GetExplorationPatternsAsync();
    }
}

