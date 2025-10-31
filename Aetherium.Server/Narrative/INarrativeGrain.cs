using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Narrative
{
    /// <summary>
    /// Orleans grain interface for managing game narratives.
    /// Each narrative has a unique ID and persists its state.
    /// </summary>
    public interface INarrativeGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Gets the complete narrative definition.
        /// </summary>
        Task<NarrativeDefinition?> GetNarrativeAsync();

        /// <summary>
        /// Sets or updates the narrative definition.
        /// </summary>
        Task SetNarrativeAsync(NarrativeDefinition narrative);

        /// <summary>
        /// Adds or updates a quest within the narrative.
        /// </summary>
        Task AddOrUpdateQuestAsync(QuestDefinition quest);

        /// <summary>
        /// Removes a quest from the narrative.
        /// </summary>
        Task RemoveQuestAsync(string questId);

        /// <summary>
        /// Adds or updates a loot table.
        /// </summary>
        Task AddOrUpdateLootTableAsync(string tableId, LootTable lootTable);

        /// <summary>
        /// Gets a specific loot table by ID.
        /// </summary>
        Task<LootTable?> GetLootTableAsync(string tableId);

        /// <summary>
        /// Adds or updates a monster density rule.
        /// </summary>
        Task AddOrUpdateMonsterDensityAsync(string zonePattern, MonsterDensityRule rule);

        /// <summary>
        /// Gets monster density for a zone pattern.
        /// </summary>
        Task<MonsterDensityRule?> GetMonsterDensityAsync(string zonePattern);

        /// <summary>
        /// Adds or updates an NPC goal definition.
        /// </summary>
        Task AddOrUpdateNPCGoalAsync(NPCGoalDefinition goal);

        /// <summary>
        /// Gets all NPC goals in the narrative.
        /// </summary>
        Task<List<NPCGoalDefinition>> GetNPCGoalsAsync();

        /// <summary>
        /// Deletes the entire narrative.
        /// </summary>
        Task DeleteAsync();

        /// <summary>
        /// Gets adaptive quests for an agent based on behavior.
        /// </summary>
        Task<List<QuestDefinition>> GetAdaptiveQuestsAsync(string agentId, int maxQuests = 5);

        /// <summary>
        /// Updates quest adaptively based on agent behavior.
        /// </summary>
        Task<QuestDefinition?> AdaptQuestForAgentAsync(string questId, string agentId);
    }
}


