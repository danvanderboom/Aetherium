using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Orleans;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Orleans grain interface for adaptive narrative generation.
    /// </summary>
    public interface IAdaptiveNarrativeGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Generates adaptive quests for an agent based on behavior analysis.
        /// </summary>
        Task<List<QuestDefinition>> GenerateAdaptiveQuestsAsync(string agentId, int maxQuests = 5);

        /// <summary>
        /// Adapts an existing quest based on agent behavior.
        /// </summary>
        Task<QuestDefinition> AdaptQuestAsync(string questId, string agentId);

        /// <summary>
        /// Gets recommended quests for an agent.
        /// </summary>
        Task<List<QuestDefinition>> GetRecommendedQuestsAsync(string agentId, int maxQuests = 5);

        /// <summary>
        /// Updates adaptive quests when agent behavior changes.
        /// </summary>
        Task UpdateAdaptiveQuestsAsync(string agentId);
    }
}

