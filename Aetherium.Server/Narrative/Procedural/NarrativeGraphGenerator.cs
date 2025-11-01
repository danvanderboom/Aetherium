using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Narrative;
using Aetherium.Server.Narrative.CrossWorld;

namespace Aetherium.Server.Narrative.Procedural
{
    /// <summary>
    /// Generates procedural quest dependency chains based on NPC needs and world state.
    /// Creates multi-stage quests (fetch/rescue/defend) with prerequisite relationships.
    /// </summary>
    public static class NarrativeGraphGenerator
    {
        /// <summary>
        /// Generates a quest chain based on NPC goals and world state.
        /// </summary>
        public static List<QuestDefinition> GenerateQuestChain(
            string baseQuestId,
            List<NPCGoalDefinition> npcGoals,
            NarrativeDefinition? narrativeDefinition,
            Random? random = null)
        {
            var rng = random ?? new Random();
            var quests = new List<QuestDefinition>();

            // Group NPC goals by type to create quest themes
            var goalsByType = npcGoals.GroupBy(g => g.GoalType).ToList();

            foreach (var goalGroup in goalsByType)
            {
                var goalType = goalGroup.Key;
                var goals = goalGroup.ToList();

                // Generate quest based on goal type
                QuestDefinition? quest = null;

                switch (goalType.ToLowerInvariant())
                {
                    case "collect_items":
                    case "fetch":
                        quest = GenerateFetchQuest($"{baseQuestId}-fetch-{rng.Next(1000)}", goals, rng);
                        break;

                    case "rescue":
                    case "save":
                        quest = GenerateRescueQuest($"{baseQuestId}-rescue-{rng.Next(1000)}", goals, rng);
                        break;

                    case "defend":
                    case "protect":
                        quest = GenerateDefendQuest($"{baseQuestId}-defend-{rng.Next(1000)}", goals, rng);
                        break;

                    default:
                        // Generic quest from first goal
                        if (goals.Count > 0)
                        {
                            quest = GenerateGenericQuest($"{baseQuestId}-generic-{rng.Next(1000)}", goals[0], rng);
                        }
                        break;
                }

                if (quest != null)
                {
                    quests.Add(quest);
                }
            }

            // Build prerequisite chains based on quest complexity
            BuildPrerequisiteChains(quests, rng);

            return quests;
        }

        /// <summary>
        /// Generates a fetch quest to retrieve items for an NPC.
        /// </summary>
        private static QuestDefinition GenerateFetchQuest(
            string questId,
            List<NPCGoalDefinition> goals,
            Random random)
        {
            var goal = goals[random.Next(goals.Count)];
            var itemTypes = goal.GenerationRequirements?.RequiredItems ?? new List<string>();

            var quest = new QuestDefinition
            {
                QuestId = questId,
                Title = $"Retrieve {GetItemDisplayName(itemTypes)}",
                Description = $"Collect the requested items and deliver them to {goal.NPCType}.",
                Objectives = new List<QuestObjective>(),
                Rewards = new Dictionary<string, string>()
            };

            // Add collect objectives
            foreach (var itemType in itemTypes)
            {
                quest.Objectives.Add(new QuestObjective
                {
                    ObjectiveId = $"collect-{itemType}",
                    Type = "collect",
                    Parameters = new Dictionary<string, object>
                    {
                        ["itemType"] = itemType,
                        ["requiredCount"] = 1
                    }
                });
            }

            // Add rewards
            quest.Rewards["Experience"] = (itemTypes.Count * 50).ToString();
            quest.Rewards["Gold"] = (itemTypes.Count * 25).ToString();

            return quest;
        }

        /// <summary>
        /// Generates a rescue quest to save an NPC or entity.
        /// </summary>
        private static QuestDefinition GenerateRescueQuest(
            string questId,
            List<NPCGoalDefinition> goals,
            Random random)
        {
            var goal = goals[random.Next(goals.Count)];

            var quest = new QuestDefinition
            {
                QuestId = questId,
                Title = $"Rescue {goal.NPCType}",
                Description = $"Find and rescue the captured {goal.NPCType}.",
                Objectives = new List<QuestObjective>(),
                Rewards = new Dictionary<string, string>()
            };

            // Add location objective (find the rescue target)
            quest.Objectives.Add(new QuestObjective
            {
                ObjectiveId = "reach-location",
                Type = "reach_location",
                Parameters = new Dictionary<string, object>
                {
                    ["locationHint"] = goal.GenerationRequirements?.PreferredZoneType ?? "dungeon"
                }
            });

            // Add defeat enemies objective
            quest.Objectives.Add(new QuestObjective
            {
                ObjectiveId = "defeat-captors",
                Type = "kill",
                Parameters = new Dictionary<string, object>
                {
                    ["enemyType"] = "Guard",
                    ["requiredCount"] = random.Next(3, 6)
                }
            });

            // Add rewards
            quest.Rewards["Experience"] = "200";
            quest.Rewards["Gold"] = "100";
            quest.Rewards["Items"] = "rescue-medal";

            return quest;
        }

        /// <summary>
        /// Generates a defend quest to protect a location or NPC.
        /// </summary>
        private static QuestDefinition GenerateDefendQuest(
            string questId,
            List<NPCGoalDefinition> goals,
            Random random)
        {
            var goal = goals[random.Next(goals.Count)];

            var quest = new QuestDefinition
            {
                QuestId = questId,
                Title = $"Defend {goal.NPCType}",
                Description = $"Protect the {goal.NPCType} from incoming threats.",
                Objectives = new List<QuestObjective>(),
                Rewards = new Dictionary<string, string>()
            };

            // Add defend location objective
            quest.Objectives.Add(new QuestObjective
            {
                ObjectiveId = "defend-location",
                Type = "defend",
                Parameters = new Dictionary<string, object>
                {
                    ["targetType"] = goal.NPCType,
                    ["duration"] = random.Next(60, 180) // seconds
                }
            });

            // Add defeat waves objective
            quest.Objectives.Add(new QuestObjective
            {
                ObjectiveId = "defeat-waves",
                Type = "kill",
                Parameters = new Dictionary<string, object>
                {
                    ["enemyType"] = "Raider",
                    ["requiredCount"] = random.Next(5, 10)
                }
            });

            // Add rewards
            quest.Rewards["Experience"] = "250";
            quest.Rewards["Gold"] = "150";
            quest.Rewards["Items"] = "defender-badge";

            return quest;
        }

        /// <summary>
        /// Generates a generic quest from an NPC goal.
        /// </summary>
        private static QuestDefinition GenerateGenericQuest(
            string questId,
            NPCGoalDefinition goal,
            Random random)
        {
            var quest = new QuestDefinition
            {
                QuestId = questId,
                Title = $"Help {goal.NPCType}",
                Description = $"Assist the {goal.NPCType} with their goal.",
                Objectives = new List<QuestObjective>(),
                Rewards = new Dictionary<string, string>()
            };

            // Convert goal objectives to quest objectives, checking for cross-world constraints
            foreach (var goalObjective in goal.Objectives)
            {
                // Check if this objective has a cross-world constraint
                var crossWorldConstraint = ExtractCrossWorldConstraint(goalObjective.Parameters);
                
                if (crossWorldConstraint != null)
                {
                    // Emit travel_to objective for cross-world travel
                    var travelObjective = CreateTravelToObjective(goalObjective.ObjectiveId, crossWorldConstraint);
                    if (travelObjective != null)
                    {
                        quest.Objectives.Add(travelObjective);
                    }
                }
                else
                {
                    // Regular objective
                    quest.Objectives.Add(new QuestObjective
                    {
                        ObjectiveId = goalObjective.ObjectiveId,
                        Type = goalObjective.Type,
                        Parameters = goalObjective.Parameters ?? new Dictionary<string, object>()
                    });
                }
            }

            // Add rewards
            quest.Rewards["Experience"] = "100";
            quest.Rewards["Gold"] = "50";

            return quest;
        }

        /// <summary>
        /// Extracts a CrossWorldConstraint from goal objective parameters.
        /// </summary>
        private static CrossWorldConstraint? ExtractCrossWorldConstraint(Dictionary<string, object>? parameters)
        {
            if (parameters == null)
                return null;

            // Check for cross-world constraint in parameters
            // Format: { "crossWorld": { "worldSelector": {...}, "mapSelector": {...} } }
            if (parameters.TryGetValue("crossWorld", out var crossWorldObj))
            {
                if (crossWorldObj is Dictionary<string, object> crossWorldDict)
                {
                    var constraint = new CrossWorldConstraint();

                    // Extract world selector
                    if (crossWorldDict.TryGetValue("worldSelector", out var worldSelObj) && 
                        worldSelObj is Dictionary<string, object> worldSelDict)
                    {
                        constraint.WorldSelector = new WorldSelector
                        {
                            WorldId = worldSelDict.TryGetValue("worldId", out var wid) ? wid?.ToString() : null,
                            WorldTag = worldSelDict.TryGetValue("worldTag", out var wtag) ? wtag?.ToString() : null,
                            WorldTemplate = worldSelDict.TryGetValue("worldTemplate", out var wtmpl) ? wtmpl?.ToString() : null
                        };

                        if (worldSelDict.TryGetValue("excludeWorldIds", out var exclObj) && 
                            exclObj is List<string> excludeList)
                        {
                            constraint.WorldSelector.ExcludeWorldIds = excludeList;
                        }
                    }

                    // Extract map selector
                    if (crossWorldDict.TryGetValue("mapSelector", out var mapSelObj) && 
                        mapSelObj is Dictionary<string, object> mapSelDict)
                    {
                        constraint.MapSelector = new MapSelector
                        {
                            MapId = mapSelDict.TryGetValue("mapId", out var mid) ? mid?.ToString() : null,
                            MapTag = mapSelDict.TryGetValue("mapTag", out var mtag) ? mtag?.ToString() : null,
                            MapName = mapSelDict.TryGetValue("mapName", out var mname) ? mname?.ToString() : null
                        };
                    }

                    // Extract requires unlock flag
                    if (crossWorldDict.TryGetValue("requiresUnlock", out var reqUnlockObj))
                    {
                        constraint.RequiresUnlock = reqUnlockObj is bool reqUnlock ? reqUnlock : 
                                                     bool.TryParse(reqUnlockObj?.ToString(), out var parsed) && parsed;
                    }

                    // Only return if we have at least a world or map selector
                    if (constraint.WorldSelector != null || constraint.MapSelector != null)
                    {
                        return constraint;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a travel_to quest objective from a cross-world constraint.
        /// </summary>
        private static QuestObjective? CreateTravelToObjective(
            string baseObjectiveId,
            CrossWorldConstraint constraint)
        {
            if (constraint == null)
                return null;

            var parameters = new Dictionary<string, object>();

            // Add world selector parameters
            if (constraint.WorldSelector != null)
            {
                var worldSelDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(constraint.WorldSelector.WorldId))
                    worldSelDict["worldId"] = constraint.WorldSelector.WorldId;
                if (!string.IsNullOrEmpty(constraint.WorldSelector.WorldTag))
                    worldSelDict["worldTag"] = constraint.WorldSelector.WorldTag;
                if (!string.IsNullOrEmpty(constraint.WorldSelector.WorldTemplate))
                    worldSelDict["worldTemplate"] = constraint.WorldSelector.WorldTemplate;
                if (constraint.WorldSelector.ExcludeWorldIds != null && constraint.WorldSelector.ExcludeWorldIds.Count > 0)
                    worldSelDict["excludeWorldIds"] = constraint.WorldSelector.ExcludeWorldIds;

                parameters["worldSelector"] = worldSelDict;
            }

            // Add map selector parameters
            if (constraint.MapSelector != null)
            {
                var mapSelDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(constraint.MapSelector.MapId))
                    mapSelDict["mapId"] = constraint.MapSelector.MapId;
                if (!string.IsNullOrEmpty(constraint.MapSelector.MapTag))
                    mapSelDict["mapTag"] = constraint.MapSelector.MapTag;
                if (!string.IsNullOrEmpty(constraint.MapSelector.MapName))
                    mapSelDict["mapName"] = constraint.MapSelector.MapName;

                parameters["mapSelector"] = mapSelDict;
            }

            // Add requires unlock flag
            if (constraint.RequiresUnlock)
            {
                parameters["requiresUnlock"] = true;
            }

            return new QuestObjective
            {
                ObjectiveId = $"{baseObjectiveId}-travel",
                Type = "travel_to",
                Parameters = parameters
            };
        }

        /// <summary>
        /// Builds prerequisite chains between quests based on complexity and type.
        /// </summary>
        private static void BuildPrerequisiteChains(List<QuestDefinition> quests, Random random)
        {
            if (quests.Count < 2)
                return;

            // Sort by quest type complexity (rescue > defend > fetch > generic)
            var sortedQuests = quests.OrderBy(q =>
            {
                if (q.Title.Contains("Rescue", StringComparison.OrdinalIgnoreCase)) return 3;
                if (q.Title.Contains("Defend", StringComparison.OrdinalIgnoreCase)) return 2;
                if (q.Title.Contains("Retrieve", StringComparison.OrdinalIgnoreCase)) return 1;
                return 0;
            }).ToList();

            // Create chains: simpler quests unlock more complex ones
            for (int i = 1; i < sortedQuests.Count; i++)
            {
                // 70% chance to chain from previous quest
                if (random.NextDouble() < 0.7)
                {
                    sortedQuests[i].PrerequisiteQuestIds.Add(sortedQuests[i - 1].QuestId);
                }
            }
        }

        /// <summary>
        /// Gets a display name for items (pluralized if multiple).
        /// </summary>
        private static string GetItemDisplayName(List<string> itemTypes)
        {
            if (itemTypes.Count == 0)
                return "Items";

            if (itemTypes.Count == 1)
                return itemTypes[0];

            return $"{itemTypes.Count} Items";
        }
    }
}

