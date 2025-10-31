using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using ConsoleGameServer.Narrative;

namespace ConsoleGame.Test
{
    [TestFixture]
    public class NarrativeGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Configure test grain storage

                // Add in-memory grain storage for testing (name must match grain attribute)
                siloBuilder.AddMemoryGrainStorage("narrativeStore");
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            SiloConfigurator.SetCluster(_cluster);
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        private NarrativeDefinition CreateTestNarrative()
        {
            return new NarrativeDefinition
            {
                NarrativeId = "test-narrative",
                Name = "Test Narrative",
                Description = "A test narrative for unit testing",
                Quests = new List<QuestDefinition>
                {
                    new QuestDefinition
                    {
                        QuestId = "quest-1",
                        Title = "First Quest",
                        Description = "Complete the first quest",
                        Objectives = new List<QuestObjective>
                        {
                            new QuestObjective
                            {
                                ObjectiveId = "obj-1",
                                Type = "kill",
                                Parameters = new Dictionary<string, object>
                                {
                                    ["target"] = "Goblin",
                                    ["count"] = 5
                                }
                            }
                        }
                    }
                },
                LootTables = new Dictionary<string, LootTable>
                {
                    ["chest"] = new LootTable
                    {
                        TableId = "chest",
                        Entries = new List<LootEntry>
                        {
                            new LootEntry { ItemType = "gold-coin", Weight = 100, MinQuantity = 10, MaxQuantity = 50 },
                            new LootEntry { ItemType = "health-potion", Weight = 50, MinQuantity = 1, MaxQuantity = 3 }
                        }
                    }
                },
                MonsterDensity = new Dictionary<string, MonsterDensityRule>
                {
                    ["forest"] = new MonsterDensityRule
                    {
                        ZonePattern = "forest",
                        MonsterTypes = new Dictionary<string, float>
                        {
                            ["Goblin"] = 0.2f,
                            ["Wolf"] = 0.1f
                        }
                    }
                },
                NPCGoals = new List<NPCGoalDefinition>
                {
                    new NPCGoalDefinition
                    {
                        GoalId = "goal-1",
                        NPCType = "guard",
                        GoalType = "patrol_route"
                    }
                }
            };
        }

        [Test]
        public async Task NarrativeGrain_ShouldSet_ValidDefinition()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var retrieved = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.NarrativeId, Is.EqualTo(narrativeId));
            Assert.That(retrieved.Name, Is.EqualTo("Test Narrative"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldRetrieve_AllQuests()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.Quests, Is.Not.Null);
            Assert.That(narrative.Quests.Count, Is.EqualTo(1));
            Assert.That(narrative.Quests[0].QuestId, Is.EqualTo("quest-1"));
            Assert.That(narrative.Quests[0].Title, Is.EqualTo("First Quest"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldAdd_Quest()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            var newQuest = new QuestDefinition
            {
                QuestId = "quest-2",
                Title = "Second Quest",
                Description = "Another quest",
                Objectives = new List<QuestObjective>()
            };

            // Act
            await grain.AddOrUpdateQuestAsync(newQuest);
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.Quests.Count, Is.EqualTo(2));
            Assert.That(narrative.Quests.Any(q => q.QuestId == "quest-2"), Is.True);
        }

        [Test]
        public async Task NarrativeGrain_ShouldRemove_Quest()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            // Act
            await grain.RemoveQuestAsync("quest-1");
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.Quests.Any(q => q.QuestId == "quest-1"), Is.False);
        }

        [Test]
        public async Task NarrativeGrain_ShouldRetrieve_LootTable()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var lootTable = await grain.GetLootTableAsync("chest");

            // Assert
            Assert.That(lootTable, Is.Not.Null);
            Assert.That(lootTable.Entries.Count, Is.EqualTo(2));
            Assert.That(lootTable.Entries[0].ItemType, Is.EqualTo("gold-coin"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldReturn_NullForInvalidLootTable()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var lootTable = await grain.GetLootTableAsync("nonexistent");

            // Assert
            Assert.That(lootTable, Is.Null);
        }

        [Test]
        public async Task NarrativeGrain_ShouldAdd_LootTable()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            var newLootTable = new LootTable
            {
                TableId = "boss-loot",
                Entries = new List<LootEntry>
                {
                    new LootEntry { ItemType = "rare-sword", Weight = 10, MinQuantity = 1, MaxQuantity = 1 }
                }
            };

            // Act
            await grain.AddOrUpdateLootTableAsync("boss-loot", newLootTable);
            var lootTable = await grain.GetLootTableAsync("boss-loot");

            // Assert
            Assert.That(lootTable, Is.Not.Null);
            Assert.That(lootTable.TableId, Is.EqualTo("boss-loot"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldRetrieve_MonsterDensity()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var rule = await grain.GetMonsterDensityAsync("forest");

            // Assert
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule.ZonePattern, Is.EqualTo("forest"));
            Assert.That(rule.MonsterTypes.ContainsKey("Goblin"), Is.True);
        }

        [Test]
        public async Task NarrativeGrain_ShouldAdd_MonsterDensityRule()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            var newRule = new MonsterDensityRule
            {
                ZonePattern = "mountain",
                MonsterTypes = new Dictionary<string, float>
                {
                    ["Dragon"] = 0.01f,
                    ["Troll"] = 0.15f
                }
            };

            // Act
            await grain.AddOrUpdateMonsterDensityAsync("mountain", newRule);
            var rule = await grain.GetMonsterDensityAsync("mountain");

            // Assert
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule.MonsterTypes.ContainsKey("Dragon"), Is.True);
        }

        [Test]
        public async Task NarrativeGrain_ShouldRetrieve_NPCGoals()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var goals = await grain.GetNPCGoalsAsync();

            // Assert
            Assert.That(goals, Is.Not.Null);
            Assert.That(goals.Count, Is.EqualTo(1));
            Assert.That(goals[0].GoalId, Is.EqualTo("goal-1"));
            Assert.That(goals[0].NPCType, Is.EqualTo("guard"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldAdd_NPCGoal()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            var newGoal = new NPCGoalDefinition
            {
                GoalId = "goal-2",
                NPCType = "merchant",
                GoalType = "trade"
            };

            // Act
            await grain.AddOrUpdateNPCGoalAsync(newGoal);
            var goals = await grain.GetNPCGoalsAsync();

            // Assert
            Assert.That(goals.Count, Is.EqualTo(2));
            Assert.That(goals.Any(g => g.GoalId == "goal-2"), Is.True);
        }

        [Test]
        public async Task NarrativeGrain_ShouldUpdate_ExistingDefinition()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            await grain.SetNarrativeAsync(definition);

            // Act - Update the definition
            definition.Name = "Updated Narrative";
            definition.Description = "Updated description";
            await grain.SetNarrativeAsync(definition);

            var retrieved = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Name, Is.EqualTo("Updated Narrative"));
            Assert.That(retrieved.Description, Is.EqualTo("Updated description"));
        }

        [Test]
        public async Task NarrativeGrain_ShouldHandle_EmptyDefinition()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = new NarrativeDefinition
            {
                NarrativeId = narrativeId,
                Name = "Empty Narrative",
                Description = "A narrative with no content"
            };

            // Act
            await grain.SetNarrativeAsync(definition);
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.Quests, Is.Empty);
            Assert.That(narrative.LootTables, Is.Empty);
            Assert.That(narrative.MonsterDensity, Is.Empty);
            Assert.That(narrative.NPCGoals, Is.Empty);
        }

        [Test]
        public async Task NarrativeGrain_ShouldDelete_Narrative()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;
            await grain.SetNarrativeAsync(definition);

            // Act
            await grain.DeleteAsync();
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Null);
        }

        [Test]
        public async Task NarrativeGrain_ShouldPreserve_QuestObjectives()
        {
            // Arrange
            var narrativeId = $"narrative-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<INarrativeGrain>(narrativeId);
            var definition = CreateTestNarrative();
            definition.NarrativeId = narrativeId;

            // Act
            await grain.SetNarrativeAsync(definition);
            var narrative = await grain.GetNarrativeAsync();

            // Assert
            Assert.That(narrative, Is.Not.Null);
            Assert.That(narrative.Quests[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(narrative.Quests[0].Objectives[0].Type, Is.EqualTo("kill"));
            Assert.That(narrative.Quests[0].Objectives[0].Parameters, Contains.Key("target"));
        }
    }
}
