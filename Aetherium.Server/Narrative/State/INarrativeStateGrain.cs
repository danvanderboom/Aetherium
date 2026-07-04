using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative.State
{
    /// <summary>
    /// Orleans grain interface for managing runtime narrative state.
    /// Supports both shared (per-narrative) and per-world state.
    /// </summary>
    public interface INarrativeStateGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Gets the current narrative state including active quests, completed quests, and consequences.
        /// </summary>
        Task<NarrativeState?> GetStateAsync();

        /// <summary>
        /// Marks a quest as completed, which may trigger follow-up quests.
        /// </summary>
        Task MarkQuestCompletedAsync(string questId);

        /// <summary>
        /// Records a world event that may trigger narrative consequences.
        /// </summary>
        Task RecordEventAsync(string eventType, Dictionary<string, object> eventData);

        /// <summary>
        /// Adds a procedurally generated quest to the state.
        /// </summary>
        Task AddGeneratedQuestAsync(QuestDefinition quest);

        /// <summary>
        /// Gets all available quests (base + generated) for a player.
        /// </summary>
        Task<List<QuestDefinition>> GetAvailableQuestsAsync();

        /// <summary>
        /// Gets all completed quest IDs.
        /// </summary>
        Task<HashSet<string>> GetCompletedQuestIdsAsync();

        /// <summary>
        /// Checks if a quest can be started (prerequisites met).
        /// </summary>
        Task<bool> CanStartQuestAsync(string questId);

        /// <summary>
        /// Activates a quest: if its prerequisites are met and it is neither active nor completed,
        /// adds it to the active set and registers its objectives for tracking. Returns true if the
        /// quest was started, false otherwise (unknown quest, already active/completed, prereqs unmet).
        /// </summary>
        Task<bool> StartQuestAsync(string questId);

        /// <summary>
        /// Gets the set of currently-active quest IDs.
        /// </summary>
        Task<HashSet<string>> GetActiveQuestIdsAsync();

        /// <summary>
        /// Gets the full definitions of the currently-active quests (resolved from the base
        /// narrative and generated quests), for building a player-facing quest log.
        /// </summary>
        Task<List<QuestDefinition>> GetActiveQuestsAsync();

        /// <summary>
        /// Updates relationship between two NPCs.
        /// </summary>
        Task UpdateRelationshipAsync(string npc1Id, string npc2Id, float relationshipValue);

        /// <summary>
        /// Gets relationship value between two NPCs.
        /// </summary>
        Task<float?> GetRelationshipAsync(string npc1Id, string npc2Id);
    }

    /// <summary>
    /// Runtime state for narrative progression including quests, events, and relationships.
    /// </summary>
    [GenerateSerializer]
    public class NarrativeState
    {
        [Id(0)] public string StateId { get; set; } = string.Empty;
        [Id(1)] public string NarrativeId { get; set; } = string.Empty;
        [Id(2)] public string? WorldId { get; set; } // null if shared across worlds
        [Id(3)] public HashSet<string> CompletedQuestIds { get; set; } = new HashSet<string>();
        [Id(4)] public HashSet<string> ActiveQuestIds { get; set; } = new HashSet<string>();
        [Id(5)] public List<QuestDefinition> GeneratedQuests { get; set; } = new List<QuestDefinition>();
        [Id(6)] public List<NarrativeEvent> Events { get; set; } = new List<NarrativeEvent>();
        [Id(7)] public Dictionary<string, Dictionary<string, float>> Relationships { get; set; } = new Dictionary<string, Dictionary<string, float>>(); // NPC ID -> (NPC ID -> relationship value)
        [Id(8)] public Dictionary<string, List<string>> ActiveQuestObjectives { get; set; } = new Dictionary<string, List<string>>(); // QuestId -> List of ObjectiveIds
        [Id(9)] public Dictionary<string, HashSet<string>> CompletedObjectives { get; set; } = new Dictionary<string, HashSet<string>>(); // QuestId -> Set of completed ObjectiveIds
        [Id(10)] public Dictionary<string, Dictionary<string, int>> ObjectiveProgress { get; set; } = new Dictionary<string, Dictionary<string, int>>(); // QuestId -> (ObjectiveId -> current count) for count-based objectives (collect/kill)
    }

    /// <summary>
    /// A recorded world event that may have narrative consequences.
    /// </summary>
    [GenerateSerializer]
    public class NarrativeEvent
    {
        [Id(0)] public string EventId { get; set; } = System.Guid.NewGuid().ToString("N");
        [Id(1)] public string EventType { get; set; } = string.Empty;
        [Id(2)] public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
        [Id(3)] public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
    }
}

