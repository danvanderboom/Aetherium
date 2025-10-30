using System.Collections.Generic;

namespace ConsoleGameServer.Narrative
{
    /// <summary>
    /// Root definition for a game narrative including quests, loot tables, and NPC goals.
    /// </summary>
    public class NarrativeDefinition
    {
        public string NarrativeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<QuestDefinition> Quests { get; set; } = new List<QuestDefinition>();
        public Dictionary<string, LootTable> LootTables { get; set; } = new Dictionary<string, LootTable>();
        public Dictionary<string, MonsterDensityRule> MonsterDensity { get; set; } = new Dictionary<string, MonsterDensityRule>();
        public List<NPCGoalDefinition> NPCGoals { get; set; } = new List<NPCGoalDefinition>();
    }

    /// <summary>
    /// Definition of a quest with objectives and rewards.
    /// </summary>
    public class QuestDefinition
    {
        public string QuestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        public Dictionary<string, string> Rewards { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// A single quest objective.
    /// </summary>
    public class QuestObjective
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "collect", "kill", "reach_location", etc.
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Loot table for random item distribution.
    /// </summary>
    public class LootTable
    {
        public string TableId { get; set; } = string.Empty;
        public List<LootEntry> Entries { get; set; } = new List<LootEntry>();
    }

    /// <summary>
    /// Single entry in a loot table with weighted probability.
    /// </summary>
    public class LootEntry
    {
        public string ItemType { get; set; } = string.Empty;
        public int Weight { get; set; }
        public int MinQuantity { get; set; } = 1;
        public int MaxQuantity { get; set; } = 1;
    }

    /// <summary>
    /// Monster spawn density rule for a zone.
    /// </summary>
    public class MonsterDensityRule
    {
        public string ZonePattern { get; set; } = string.Empty; // "outdoor", "city:district-1", "*"
        public Dictionary<string, float> MonsterTypes { get; set; } = new Dictionary<string, float>(); // Type -> spawns per 100 tiles
    }

    /// <summary>
    /// NPC goal that can influence world generation and runtime behavior.
    /// </summary>
    public class NPCGoalDefinition
    {
        public string GoalId { get; set; } = string.Empty;
        public string NPCType { get; set; } = string.Empty; // "merchant", "guard", "bandit", etc.
        public string GoalType { get; set; } = string.Empty; // "guard_location", "collect_items", "patrol_route"
        public List<GoalObjective> Objectives { get; set; } = new List<GoalObjective>();
        public GenerationRequirements GenerationRequirements { get; set; } = new GenerationRequirements();
    }

    /// <summary>
    /// Objective for an NPC goal.
    /// </summary>
    public class GoalObjective
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "stay_at", "patrol", "collect", "trade"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public bool IsActive { get; set; } = true;
        public List<string> TriggerConditions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Requirements for world generation to fulfill NPC goals.
    /// </summary>
    public class GenerationRequirements
    {
        public List<string> RequiredPrefabs { get; set; } = new List<string>(); // e.g., ["guard-tower", "barracks"]
        public Dictionary<string, PrefabPlacementHint> PrefabPlacement { get; set; } = new Dictionary<string, PrefabPlacementHint>();
        public List<string> RequiredItems { get; set; } = new List<string>();
        public Dictionary<string, WorldFeatureRequest> FeatureRequests { get; set; } = new Dictionary<string, WorldFeatureRequest>();
        public string PreferredZoneType { get; set; } = string.Empty; // "city", "outdoor", "dungeon"
    }

    /// <summary>
    /// Hint for prefab placement relative to NPC spawn.
    /// </summary>
    public class PrefabPlacementHint
    {
        public string PrefabId { get; set; } = string.Empty;
        public string PlacementRule { get; set; } = string.Empty; // "adjacent", "within_10", "line_of_sight"
        public int Priority { get; set; }
    }

    /// <summary>
    /// Request for a world feature (patrol path, safe zone, etc.).
    /// </summary>
    public class WorldFeatureRequest
    {
        public string FeatureType { get; set; } = string.Empty; // "patrol_path", "safe_zone", "trade_route"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}

