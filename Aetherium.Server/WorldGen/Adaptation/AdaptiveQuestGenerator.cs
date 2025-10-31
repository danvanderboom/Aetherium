using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.Narrative;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Generates adaptive quests based on agent behavior and telemetry.
    /// </summary>
    public static class AdaptiveQuestGenerator
    {
        /// <summary>
        /// Generates a quest based on agent behavior analysis and content needs.
        /// </summary>
        public static QuestDefinition GenerateQuest(
            string questId,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile = null)
        {
            var quest = new QuestDefinition
            {
                QuestId = questId,
                Title = GenerateQuestTitle(behaviorAnalysis, contentNeeds),
                Description = GenerateQuestDescription(behaviorAnalysis, contentNeeds),
                Objectives = new List<QuestObjective>(),
                Rewards = new Dictionary<string, string>()
            };

            // Generate objectives based on content needs and weaknesses
            var objectives = GenerateObjectives(behaviorAnalysis, contentNeeds, interestProfile);
            quest.Objectives.AddRange(objectives);

            // Generate rewards based on agent needs
            quest.Rewards = GenerateRewards(contentNeeds, interestProfile);

            return quest;
        }

        /// <summary>
        /// Generates multiple quests to address different needs.
        /// </summary>
        public static List<QuestDefinition> GenerateQuests(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile = null,
            int maxQuests = 5)
        {
            var quests = new List<QuestDefinition>();

            // Generate a quest for each high-priority content need
            var highPriorityNeeds = contentNeeds
                .Where(n => n.Priority >= 0.5)
                .OrderByDescending(n => n.Priority)
                .Take(maxQuests)
                .ToList();

            for (int i = 0; i < highPriorityNeeds.Count; i++)
            {
                var need = highPriorityNeeds[i];
                var questId = $"adaptive-quest-{DateTime.UtcNow:yyyyMMddHHmmss}-{i}";
                var quest = GenerateQuest(questId, behaviorAnalysis, new List<ContentNeed> { need }, interestProfile);
                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Adapts an existing quest based on agent progress and behavior.
        /// </summary>
        public static QuestDefinition AdaptQuest(
            QuestDefinition existingQuest,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var adaptedQuest = new QuestDefinition
            {
                QuestId = existingQuest.QuestId,
                Title = existingQuest.Title,
                Description = existingQuest.Description,
                Objectives = new List<QuestObjective>(),
                Rewards = existingQuest.Rewards
            };

            // Simplify objectives if agent struggles
            foreach (var objective in existingQuest.Objectives)
            {
                var adaptedObjective = AdaptObjective(objective, behaviorAnalysis, contentNeeds);
                adaptedQuest.Objectives.Add(adaptedObjective);
            }

            // Adjust rewards based on current needs
            adaptedQuest.Rewards = MergeRewards(adaptedQuest.Rewards, GenerateRewards(contentNeeds, null));

            return adaptedQuest;
        }

        private static string GenerateQuestTitle(BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            if (contentNeeds.Count == 0)
            {
                return "Practice Quest";
            }

            var primaryNeed = contentNeeds.OrderByDescending(n => n.Priority).First();

            return primaryNeed.NeedType switch
            {
                "navigation_assistance" => "Pathfinder's Challenge",
                "key_lock_assistance" => "Key Master's Trial",
                "combat_assistance" => "Combat Training",
                "puzzle_assistance" => "Puzzle Practice",
                "perception_simplification" => "Simple Exploration",
                _ => "Adaptive Training Quest"
            };
        }

        private static string GenerateQuestDescription(BehaviorAnalysis behaviorAnalysis, List<ContentNeed> contentNeeds)
        {
            if (contentNeeds.Count == 0)
            {
                return "A quest designed to help you practice your skills.";
            }

            var primaryNeed = contentNeeds.OrderByDescending(n => n.Priority).First();
            return primaryNeed.Description;
        }

        private static List<QuestObjective> GenerateObjectives(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile)
        {
            var objectives = new List<QuestObjective>();

            foreach (var need in contentNeeds.OrderByDescending(n => n.Priority).Take(3))
            {
                var objective = GenerateObjectiveForNeed(need, behaviorAnalysis, interestProfile);
                if (objective != null)
                {
                    objectives.Add(objective);
                }
            }

            // If no specific needs, generate practice objectives
            if (objectives.Count == 0)
            {
                objectives.Add(new QuestObjective
                {
                    ObjectiveId = Guid.NewGuid().ToString("N"),
                    Type = "explore",
                    Parameters = new Dictionary<string, object>
                    {
                        ["areaCount"] = 5,
                        ["description"] = "Explore 5 different areas"
                    }
                });
            }

            return objectives;
        }

        private static QuestObjective? GenerateObjectiveForNeed(
            ContentNeed need,
            BehaviorAnalysis behaviorAnalysis,
            InterestProfile? interestProfile)
        {
            var objectiveId = Guid.NewGuid().ToString("N");

            return need.NeedType switch
            {
                "navigation_assistance" => new QuestObjective
                {
                    ObjectiveId = objectiveId,
                    Type = "visit_location",
                    Parameters = new Dictionary<string, object>
                    {
                        ["targetCount"] = 3,
                        ["description"] = "Visit 3 waypoints to practice navigation",
                        ["hint"] = "Follow the markers to each location"
                    }
                },
                "key_lock_assistance" => new QuestObjective
                {
                    ObjectiveId = objectiveId,
                    Type = "collect_items",
                    Parameters = new Dictionary<string, object>
                    {
                        ["targetItem"] = "key",
                        ["requiredCount"] = 1,
                        ["description"] = "Find and collect a key",
                        ["hint"] = "Look for keys near doors"
                    }
                },
                "combat_assistance" => new QuestObjective
                {
                    ObjectiveId = objectiveId,
                    Type = "defeat_enemies",
                    Parameters = new Dictionary<string, object>
                    {
                        ["targetEnemyType"] = "monster",
                        ["requiredCount"] = 1,
                        ["description"] = "Defeat a single enemy",
                        ["hint"] = "Use combat tools if available"
                    }
                },
                "puzzle_assistance" => new QuestObjective
                {
                    ObjectiveId = objectiveId,
                    Type = "interact",
                    Parameters = new Dictionary<string, object>
                    {
                        ["targetType"] = "puzzle",
                        ["description"] = "Interact with a simple puzzle",
                        ["hint"] = "Try different interactions to solve it"
                    }
                },
                _ => null
            };
        }

        private static QuestObjective AdaptObjective(
            QuestObjective objective,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var adaptedObjective = new QuestObjective
            {
                ObjectiveId = objective.ObjectiveId,
                Type = objective.Type,
                Parameters = new Dictionary<string, object>(objective.Parameters)
            };

            // Simplify if agent struggles with this type
            var strugglePatterns = behaviorAnalysis.StrugglePatterns
                .Where(s => s.ContextType.Contains(objective.Type.ToLower()))
                .ToList();

            if (strugglePatterns.Count > 0)
            {
                // Reduce requirements
                if (adaptedObjective.Parameters.ContainsKey("requiredCount"))
                {
                    var count = Convert.ToInt32(adaptedObjective.Parameters["requiredCount"]);
                    adaptedObjective.Parameters["requiredCount"] = Math.Max(1, count - 1);
                }

                // Add hints
                if (!adaptedObjective.Parameters.ContainsKey("hint"))
                {
                    adaptedObjective.Parameters["hint"] = "Take your time and practice this skill";
                }
            }

            return adaptedObjective;
        }

        private static Dictionary<string, string> GenerateRewards(
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile)
        {
            var rewards = new Dictionary<string, string>();

            // Add rewards that address current needs
            foreach (var need in contentNeeds.Take(2))
            {
                if (need.NeedType == "combat_assistance" && !rewards.ContainsKey("Items"))
                {
                    rewards["Items"] = "combat_tool";
                }
                else if (need.NeedType == "key_lock_assistance" && !rewards.ContainsKey("Items"))
                {
                    rewards["Items"] = "lockpick";
                }
                else if (need.NeedType == "navigation_assistance" && !rewards.ContainsKey("Items"))
                {
                    rewards["Items"] = "compass";
                }
            }

            // Add experience reward
            rewards["Experience"] = "100";

            return rewards;
        }

        private static Dictionary<string, string> MergeRewards(
            Dictionary<string, string> existingRewards,
            Dictionary<string, string> newRewards)
        {
            var merged = new Dictionary<string, string>(existingRewards);

            foreach (var kvp in newRewards)
            {
                if (!merged.ContainsKey(kvp.Key))
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }

            return merged;
        }
    }
}

