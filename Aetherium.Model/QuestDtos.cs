using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// Player-facing summary of a quest and its objective progress. Standalone (no server types)
    /// so it can cross the SignalR boundary; the server maps its QuestDefinition onto this.
    /// </summary>
    [GenerateSerializer]
    public class QuestSummaryDto
    {
        [Id(0)] public string QuestId { get; set; } = string.Empty;
        [Id(1)] public string Title { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public List<QuestObjectiveDto> Objectives { get; set; } = new();
        [Id(4)] public bool IsActive { get; set; }
        [Id(5)] public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Player-facing view of a single quest objective, including progress for count-based
    /// objectives (collect/kill).
    /// </summary>
    [GenerateSerializer]
    public class QuestObjectiveDto
    {
        [Id(0)] public string ObjectiveId { get; set; } = string.Empty;
        [Id(1)] public string Type { get; set; } = string.Empty;
        [Id(2)] public bool Completed { get; set; }
        [Id(3)] public int Progress { get; set; }
        [Id(4)] public int Required { get; set; } = 1;
    }

    /// <summary>
    /// The player's quest log: active quests (with progress) and completed quest IDs.
    /// </summary>
    [GenerateSerializer]
    public class QuestLogDto
    {
        [Id(0)] public List<QuestSummaryDto> Active { get; set; } = new();
        [Id(1)] public List<string> Completed { get; set; } = new();
    }
}
