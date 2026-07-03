using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Aetherium.Server.Narrative;
using Aetherium.Server.Narrative.Procedural;
using Aetherium.Server.Narrative.State;

namespace Aetherium.Server.Narrative.Consequence
{
    /// <summary>
    /// Engine that propagates player actions to generate new story branches.
    /// Processes world events and generates follow-up quests based on consequences.
    /// </summary>
    public class NarrativeConsequenceEngine
    {
        private readonly IGrainFactory _grainFactory;

        public NarrativeConsequenceEngine(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        }

        /// <summary>
        /// Processes a world event and generates narrative consequences.
        /// </summary>
        public async Task ProcessEventAsync(
            string worldId,
            string narrativeId,
            string eventType,
            Dictionary<string, object> eventData,
            string? narrativeStateScope = null)
        {
            // Get narrative state grain
            var stateGrainKey = GetStateGrainKey(worldId, narrativeId, narrativeStateScope);
            var stateGrain = _grainFactory.GetGrain<INarrativeStateGrain>(stateGrainKey);

            // Record the event
            await stateGrain.RecordEventAsync(eventType, eventData);

            // Check if event triggers quest generation
            var generatedQuests = await GenerateConsequenceQuestsAsync(
                narrativeId,
                eventType,
                eventData,
                stateGrain);

            // Add generated quests to state
            foreach (var quest in generatedQuests)
            {
                await stateGrain.AddGeneratedQuestAsync(quest);
            }
        }

        /// <summary>
        /// Generates follow-up quests based on event consequences.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateConsequenceQuestsAsync(
            string narrativeId,
            string eventType,
            Dictionary<string, object> eventData,
            INarrativeStateGrain stateGrain)
        {
            var quests = new List<QuestDefinition>();
            var narrativeGrain = _grainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var narrative = await narrativeGrain.GetNarrativeAsync();

            if (narrative == null)
                return quests;

            var completedQuests = await stateGrain.GetCompletedQuestIdsAsync();
            var random = new Random(GetSeedForEvent(eventType, eventData));

            // Generate quests based on event type
            switch (eventType.ToLowerInvariant())
            {
                case "quest_completed":
                    quests.AddRange(await GenerateFollowUpQuestsAsync(narrative, completedQuests, random));
                    break;

                case "npc_rescued":
                    quests.AddRange(await GenerateRescueConsequenceQuestsAsync(narrative, eventData, random));
                    break;

                case "location_discovered":
                    quests.AddRange(await GenerateDiscoveryQuestsAsync(narrative, eventData, random));
                    break;

                case "item_collected":
                    quests.AddRange(await GenerateCollectionQuestsAsync(narrative, eventData, random));
                    break;

                case "enemy_defeated":
                    quests.AddRange(await GenerateRevengeQuestsAsync(narrative, eventData, random));
                    break;
            }

            return quests;
        }

        /// <summary>
        /// Generates follow-up quests after quest completion.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateFollowUpQuestsAsync(
            NarrativeDefinition narrative,
            HashSet<string> completedQuests,
            Random random)
        {
            var quests = new List<QuestDefinition>();

            // Find NPC goals that might benefit from completed quest
            var npcGoals = narrative.NPCGoals ?? new List<NPCGoalDefinition>();
            if (npcGoals.Count == 0)
                return quests;

            // Generate a thank-you or follow-up quest from grateful NPCs
            if (random.NextDouble() < 0.5) // 50% chance
            {
                var goal = npcGoals[random.Next(npcGoals.Count)];
                var questId = $"followup-{Guid.NewGuid():N}";

                var quest = new QuestDefinition
                {
                    QuestId = questId,
                    Title = $"Repayment from {goal.NPCType}",
                    Description = $"The {goal.NPCType} wishes to reward you for your help.",
                    Objectives = new List<QuestObjective>(),
                    Rewards = new Dictionary<string, string>
                    {
                        ["Experience"] = "150",
                        ["Gold"] = "75",
                        ["Items"] = "gratitude-token"
                    },
                    PrerequisiteQuestIds = new List<string>(completedQuests.Take(1))
                };

                // Simple "talk to NPC" objective
                quest.Objectives.Add(new QuestObjective
                {
                    ObjectiveId = "talk-to-npc",
                    Type = "talk_to",
                    Parameters = new Dictionary<string, object>
                    {
                        ["npcType"] = goal.NPCType
                    }
                });

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Generates quests as consequences of rescuing an NPC.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateRescueConsequenceQuestsAsync(
            NarrativeDefinition narrative,
            Dictionary<string, object> eventData,
            Random random)
        {
            var quests = new List<QuestDefinition>();

            // Rescued NPC might have information about other captives
            if (random.NextDouble() < 0.7) // 70% chance
            {
                var questId = $"rescue-chain-{Guid.NewGuid():N}";

                var quest = new QuestDefinition
                {
                    QuestId = questId,
                    Title = "More Captives",
                    Description = "The rescued NPC tells you about others who need help.",
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            ObjectiveId = "find-captives",
                            Type = "reach_location",
                            Parameters = new Dictionary<string, object>
                            {
                                ["locationHint"] = "dungeon"
                            }
                        },
                        new QuestObjective
                        {
                            ObjectiveId = "rescue-others",
                            Type = "rescue",
                            Parameters = new Dictionary<string, object>
                            {
                                ["count"] = random.Next(2, 5)
                            }
                        }
                    },
                    Rewards = new Dictionary<string, string>
                    {
                        ["Experience"] = "300",
                        ["Gold"] = "150",
                        ["Items"] = "hero-badge"
                    }
                };

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Generates quests from discovering a location.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateDiscoveryQuestsAsync(
            NarrativeDefinition narrative,
            Dictionary<string, object> eventData,
            Random random)
        {
            var quests = new List<QuestDefinition>();

            // Discovery might reveal secrets that need investigation
            if (random.NextDouble() < 0.6) // 60% chance
            {
                var questId = $"investigate-{Guid.NewGuid():N}";

                var quest = new QuestDefinition
                {
                    QuestId = questId,
                    Title = "Investigate the Discovery",
                    Description = "This newly discovered location holds secrets worth exploring.",
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            ObjectiveId = "explore",
                            Type = "explore",
                            Parameters = new Dictionary<string, object>
                            {
                                ["areaSize"] = random.Next(10, 30)
                            }
                        }
                    },
                    Rewards = new Dictionary<string, string>
                    {
                        ["Experience"] = "200",
                        ["Gold"] = "100"
                    }
                };

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Generates quests from collecting important items.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateCollectionQuestsAsync(
            NarrativeDefinition narrative,
            Dictionary<string, object> eventData,
            Random random)
        {
            var quests = new List<QuestDefinition>();

            // Collected item might be part of a set
            if (eventData.TryGetValue("itemType", out var itemTypeObj))
            {
                var itemType = itemTypeObj?.ToString();
                if (!string.IsNullOrEmpty(itemType) && random.NextDouble() < 0.5) // 50% chance
                {
                    var questId = $"collection-set-{Guid.NewGuid():N}";

                    var quest = new QuestDefinition
                    {
                        QuestId = questId,
                        Title = $"Complete the {itemType} Set",
                        Description = $"You've found one piece of a valuable set. Collect the others.",
                        Objectives = new List<QuestObjective>
                        {
                            new QuestObjective
                            {
                                ObjectiveId = "collect-set",
                                Type = "collect",
                                Parameters = new Dictionary<string, object>
                                {
                                    ["itemType"] = itemType,
                                    ["requiredCount"] = random.Next(2, 5)
                                }
                            }
                        },
                        Rewards = new Dictionary<string, string>
                        {
                            ["Experience"] = "250",
                            ["Gold"] = "125",
                            ["Items"] = $"{itemType}-complete"
                        }
                    };

                    quests.Add(quest);
                }
            }

            return quests;
        }

        /// <summary>
        /// Generates revenge quests from defeating enemies.
        /// </summary>
        private async Task<List<QuestDefinition>> GenerateRevengeQuestsAsync(
            NarrativeDefinition narrative,
            Dictionary<string, object> eventData,
            Random random)
        {
            var quests = new List<QuestDefinition>();

            // Defeated enemies might have allies seeking revenge
            if (random.NextDouble() < 0.4) // 40% chance
            {
                var questId = $"revenge-{Guid.NewGuid():N}";

                var quest = new QuestDefinition
                {
                    QuestId = questId,
                    Title = "Revenge of the Fallen",
                    Description = "Allies of the defeated seek revenge for your actions.",
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            ObjectiveId = "defend",
                            Type = "defend",
                            Parameters = new Dictionary<string, object>
                            {
                                ["duration"] = random.Next(120, 300)
                            }
                        },
                        new QuestObjective
                        {
                            ObjectiveId = "defeat-avengers",
                            Type = "kill",
                            Parameters = new Dictionary<string, object>
                            {
                                ["enemyType"] = "Avenger",
                                ["requiredCount"] = random.Next(3, 7)
                            }
                        }
                    },
                    Rewards = new Dictionary<string, string>
                    {
                        ["Experience"] = "350",
                        ["Gold"] = "175"
                    }
                };

                quests.Add(quest);
            }

            return quests;
        }

        /// <summary>
        /// Gets deterministic seed for event-based generation.
        /// </summary>
        private int GetSeedForEvent(string eventType, Dictionary<string, object> eventData)
        {
            // Use a stable hash, not string.GetHashCode(): the latter is randomized per
            // process in .NET Core, which made event-driven generation non-reproducible
            // across server restarts. Hash value objects via their string form so ints/enums
            // are stable too, and order keys with an ordinal (culture-independent) comparer.
            var seed = StableHash(eventType);

            foreach (var kvp in eventData.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                seed ^= StableHash(kvp.Key);
                seed ^= StableHash(kvp.Value?.ToString() ?? string.Empty);
            }

            return seed;
        }

        /// <summary>
        /// Deterministic 32-bit FNV-1a hash of a string. Stable across processes and runs,
        /// unlike <see cref="string.GetHashCode()"/>.
        /// </summary>
        private static int StableHash(string value)
        {
            unchecked
            {
                const uint fnvOffsetBasis = 2166136261;
                const uint fnvPrime = 16777619;
                uint hash = fnvOffsetBasis;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }
                return (int)hash;
            }
        }

        /// <summary>
        /// Gets the grain key for narrative state based on scope.
        /// </summary>
        private string GetStateGrainKey(string worldId, string narrativeId, string? scope)
        {
            return scope == "per-world" ? $"{worldId}:{narrativeId}" : narrativeId;
        }
    }
}

