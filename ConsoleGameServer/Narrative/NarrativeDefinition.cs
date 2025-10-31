using System.Collections.Generic;
using Orleans;

namespace ConsoleGameServer.Narrative
{
    /// <summary>
    /// Root definition for a game narrative including quests, loot tables, and NPC goals.
    /// </summary>
    [GenerateSerializer]
    public class NarrativeDefinition
    {
        [Id(0)] public string NarrativeId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public List<QuestDefinition> Quests { get; set; } = new List<QuestDefinition>();
        [Id(4)] public Dictionary<string, LootTable> LootTables { get; set; } = new Dictionary<string, LootTable>();
        [Id(5)] public Dictionary<string, MonsterDensityRule> MonsterDensity { get; set; } = new Dictionary<string, MonsterDensityRule>();
        [Id(6)] public List<NPCGoalDefinition> NPCGoals { get; set; } = new List<NPCGoalDefinition>();
    }

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

    /// <summary>
    /// Loot table for random item distribution.
    /// </summary>
    [GenerateSerializer]
    public class LootTable
    {
        [Id(0)] public string TableId { get; set; } = string.Empty;
        [Id(1)] public List<LootEntry> Entries { get; set; } = new List<LootEntry>();
    }

    /// <summary>
    /// Single entry in a loot table with weighted probability.
    /// </summary>
    [GenerateSerializer]
    public class LootEntry
    {
        [Id(0)] public string ItemType { get; set; } = string.Empty;
        [Id(1)] public int Weight { get; set; }
        [Id(2)] public int MinQuantity { get; set; } = 1;
        [Id(3)] public int MaxQuantity { get; set; } = 1;
    }

    /// <summary>
    /// Monster spawn density rule for a zone.
    /// </summary>
    [GenerateSerializer]
    public class MonsterDensityRule
    {
        [Id(0)] public string ZonePattern { get; set; } = string.Empty; // "outdoor", "city:district-1", "*"
        [Id(1)] public Dictionary<string, float> MonsterTypes { get; set; } = new Dictionary<string, float>(); // Type -> spawns per 100 tiles
    }

    /// <summary>
    /// NPC goal that can influence world generation and runtime behavior.
    /// </summary>
    [GenerateSerializer]
    public class NPCGoalDefinition
    {
        [Id(0)] public string GoalId { get; set; } = string.Empty;
        [Id(1)] public string NPCType { get; set; } = string.Empty; // "merchant", "guard", "bandit", etc.
        [Id(2)] public string GoalType { get; set; } = string.Empty; // "guard_location", "collect_items", "patrol_route"
        [Id(3)] public List<GoalObjective> Objectives { get; set; } = new List<GoalObjective>();
        [Id(4)] public GenerationRequirements GenerationRequirements { get; set; } = new GenerationRequirements();
    }

    /// <summary>
    /// Objective for an NPC goal.
    /// </summary>
    [GenerateSerializer]
    public class GoalObjective
    {
        [Id(0)] public string ObjectiveId { get; set; } = string.Empty;
        [Id(1)] public string Type { get; set; } = string.Empty; // "stay_at", "patrol", "collect", "trade"
        [Id(2)] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        [Id(3)] public bool IsActive { get; set; } = true;
        [Id(4)] public List<string> TriggerConditions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Requirements for world generation to fulfill NPC goals.
    /// </summary>
    [GenerateSerializer]
    public class GenerationRequirements
    {
        [Id(0)] public List<string> RequiredPrefabs { get; set; } = new List<string>(); // e.g., ["guard-tower", "barracks"]
        [Id(1)] public Dictionary<string, PrefabPlacementHint> PrefabPlacement { get; set; } = new Dictionary<string, PrefabPlacementHint>();
        [Id(2)] public List<string> RequiredItems { get; set; } = new List<string>();
        [Id(3)] public Dictionary<string, WorldFeatureRequest> FeatureRequests { get; set; } = new Dictionary<string, WorldFeatureRequest>();
        [Id(4)] public string PreferredZoneType { get; set; } = string.Empty; // "city", "outdoor", "dungeon"
    }

    /// <summary>
    /// Hint for prefab placement relative to NPC spawn.
    /// </summary>
    [GenerateSerializer]
    public class PrefabPlacementHint
    {
        [Id(0)] public string PrefabId { get; set; } = string.Empty;
        [Id(1)] public string PlacementRule { get; set; } = string.Empty; // "adjacent", "within_10", "line_of_sight"
        [Id(2)] public int Priority { get; set; }
    }

    /// <summary>
    /// Request for a world feature (patrol path, safe zone, etc.).
    /// </summary>
    [GenerateSerializer]
    public class WorldFeatureRequest
    {
        [Id(0)] public string FeatureType { get; set; } = string.Empty; // "patrol_path", "safe_zone", "trade_route"
        [Id(1)] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}

