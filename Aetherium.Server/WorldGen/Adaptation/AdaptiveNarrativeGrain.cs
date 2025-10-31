using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.Narrative;
using Orleans;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Orleans grain implementation for adaptive narrative generation.
    /// </summary>
    public class AdaptiveNarrativeGrain : Grain, IAdaptiveNarrativeGrain
    {
        private readonly Dictionary<string, QuestDefinition> _adaptiveQuests = new Dictionary<string, QuestDefinition>();

        public override Task OnActivateAsync(System.Threading.CancellationToken cancellationToken)
        {
            var narrativeId = this.GetPrimaryKeyString();
            Console.WriteLine($"[AdaptiveNarrativeGrain] Activated for narrative: {narrativeId}");
            return base.OnActivateAsync(cancellationToken);
        }

        public async Task<List<QuestDefinition>> GenerateAdaptiveQuestsAsync(string agentId, int maxQuests = 5)
        {
            // Get behavior analysis for agent
            var behaviorGrain = GrainFactory.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var behaviorAnalysis = await behaviorGrain.AnalyzeBehaviorAsync();

            // Get content needs
            var contentNeeds = await behaviorGrain.GetContentNeedsAsync();

            // Get interest profile
            var interestProfile = await behaviorGrain.GetInterestProfileAsync();

            // Generate adaptive quests
            var quests = AdaptiveQuestGenerator.GenerateQuests(behaviorAnalysis, contentNeeds, interestProfile, maxQuests);

            // Store generated quests
            foreach (var quest in quests)
            {
                _adaptiveQuests[quest.QuestId] = quest;
            }

            return quests;
        }

        public async Task<QuestDefinition> AdaptQuestAsync(string questId, string agentId)
        {
            // Get existing quest
            if (!_adaptiveQuests.TryGetValue(questId, out var existingQuest))
            {
                // Try to get from narrative grain
                var narrativeId = this.GetPrimaryKeyString();
                var narrativeGrain = GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
                var narrative = await narrativeGrain.GetNarrativeAsync();
                if (narrative != null)
                {
                    existingQuest = narrative.Quests.FirstOrDefault(q => q.QuestId == questId);
                }
            }

            if (existingQuest == null)
            {
                throw new InvalidOperationException($"Quest {questId} not found");
            }

            // Get behavior analysis
            var behaviorGrain = GrainFactory.GetGrain<IBehaviorAnalysisGrain>(agentId);
            var behaviorAnalysis = await behaviorGrain.AnalyzeBehaviorAsync();
            var contentNeeds = await behaviorGrain.GetContentNeedsAsync();

            // Adapt quest
            var adaptedQuest = AdaptiveQuestGenerator.AdaptQuest(existingQuest, behaviorAnalysis, contentNeeds);

            // Store adapted quest
            _adaptiveQuests[questId] = adaptedQuest;

            return adaptedQuest;
        }

        public async Task<List<QuestDefinition>> GetRecommendedQuestsAsync(string agentId, int maxQuests = 5)
        {
            // Generate new adaptive quests if needed
            if (_adaptiveQuests.Count == 0)
            {
                await GenerateAdaptiveQuestsAsync(agentId, maxQuests);
            }

            // Return stored adaptive quests
            return _adaptiveQuests.Values.Take(maxQuests).ToList();
        }

        public async Task UpdateAdaptiveQuestsAsync(string agentId)
        {
            // Invalidate existing adaptive quests
            _adaptiveQuests.Clear();

            // Generate new adaptive quests
            await GenerateAdaptiveQuestsAsync(agentId);
        }
    }
}

