using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.Narrative
{
    // NOTE: QuestDefinition and QuestObjective (shared contracts) live in Aetherium.Model
    // (namespace retained) so clients such as Aetherium.Dashboard can deserialize adaptive-quest
    // payloads without referencing Aetherium.Server. The rest of the narrative-definition cluster
    // (NarrativeDefinition, LootTable, NPC goals, …) stays in Aetherium.Server.
    // See openspec/changes/move-contracts-to-model.

    /// <summary>
    /// Definition of a quest with objectives and rewards.
    /// </summary>
    [GenerateSerializer]
    public class QuestDefinition
    {
        [Id(0)] public string QuestId { get; set; } = string.Empty;
        [Id(1)] public string Title { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        [Id(4)] public Dictionary<string, string> Rewards { get; set; } = new Dictionary<string, string>();
        [Id(5)] public List<string> PrerequisiteQuestIds { get; set; } = new List<string>(); // Quest IDs that must be completed first
    }

    /// <summary>
    /// A single quest objective.
    /// </summary>
    [GenerateSerializer]
    public class QuestObjective
    {
        [Id(0)] public string ObjectiveId { get; set; } = string.Empty;
        [Id(1)] public string Type { get; set; } = string.Empty; // "collect", "kill", "reach_location", etc.
        [Id(2)] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}
