using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Narrative;

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

            // Convert goal objectives to quest objectives
            foreach (var goalObjective in goal.Objectives)
            {
                quest.Objectives.Add(new QuestObjective
                {
                    ObjectiveId = goalObjective.ObjectiveId,
                    Type = goalObjective.Type,
                    Parameters = goalObjective.Parameters ?? new Dictionary<string, object>()
                });
            }

            // Add rewards
            quest.Rewards["Experience"] = "100";
            quest.Rewards["Gold"] = "50";

            return quest;
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

