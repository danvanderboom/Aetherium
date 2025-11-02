using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Aetherium.Server.MetaProgression;

namespace Aetherium.Test.Orleans
{
    [TestFixture]
    public class MetaProgressionGrainTests
    {
        private TestCluster? _cluster;

        [SetUp]
        public async Task SetUp()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                _cluster.Dispose();
            }
        }

        [Test]
        public async Task MetaProgressionGrain_RecordDiscovery_TracksWorldVisit()
        {
            // Arrange
            var playerId = "player-1";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Act
            await grain.RecordDiscoveryAsync("world-1", "map-1", "outdoor", new List<string> { "forest", "hub" });

            // Assert
            var state = await grain.GetStateAsync();
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.VisitedWorldIds, Contains.Item("world-1"));
            Assert.That(state.VisitedMapIds, Contains.Item("map-1"));
            Assert.That(state.DiscoveredWorldTemplates, Contains.Item("outdoor"));
            Assert.That(state.DiscoveredTags, Contains.Item("forest"));
            Assert.That(state.DiscoveredTags, Contains.Item("hub"));
        }

        [Test]
        public async Task MetaProgressionGrain_RecordQuestCompletion_TracksQuest()
        {
            // Arrange
            var playerId = "player-2";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Act
            await grain.RecordQuestCompletionAsync("quest-1", isCrossWorld: true);

            // Assert
            var state = await grain.GetStateAsync();
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.CompletedQuestIds, Contains.Item("quest-1"));
            Assert.That(state.CompletedCrossWorldQuestIds, Contains.Item("quest-1"));
        }

        [Test]
        public async Task MetaProgressionGrain_GetAllowedGenerators_ReturnsDefaultUnlocks()
        {
            // Arrange
            var playerId = "player-3";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Act
            var generators = await grain.GetAllowedGeneratorsAsync();

            // Assert
            Assert.That(generators, Is.Not.Null);
            Assert.That(generators, Has.Count.GreaterThan(0));
            // Default unlocks should include basic generators
            Assert.That(generators, Contains.Item("PerlinTerrain").Or.Contains("BasicDungeon"));
        }

        [Test]
        public async Task MetaProgressionGrain_IsGeneratorUnlocked_ChecksUnlockStatus()
        {
            // Arrange
            var playerId = "player-4";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Act
            var isUnlocked = await grain.IsGeneratorUnlockedAsync("PerlinTerrain");

            // Assert
            Assert.That(isUnlocked, Is.True); // Default unlock
        }

        [Test]
        public async Task MetaProgressionGrain_AddUnlockCriteria_StoresCriteria()
        {
            // Arrange
            var playerId = "player-5";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            var criteria = new UnlockCriteria
            {
                CriteriaId = "criteria-1",
                UnlocksGenerator = "AdvancedDungeon",
                MinWorldVisits = 5,
                RequiredTag = "dungeon"
            };

            // Act
            await grain.AddUnlockCriteriaAsync(criteria);

            // Assert
            var state = await grain.GetStateAsync();
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.UnlockCriteriaDefinitions.ContainsKey("criteria-1"), Is.True);
        }

        [Test]
        public async Task MetaProgressionGrain_EvaluateUnlocks_UnlocksWhenCriteriaMet()
        {
            // Arrange
            var playerId = "player-6";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Add criteria that requires 2 world visits
            var criteria = new UnlockCriteria
            {
                CriteriaId = "criteria-2",
                UnlocksGenerator = "TestGenerator",
                MinWorldVisits = 2
            };

            await grain.AddUnlockCriteriaAsync(criteria);

            // Act - visit worlds to meet criteria
            await grain.RecordDiscoveryAsync("world-1", "map-1");
            await grain.RecordDiscoveryAsync("world-2", "map-2");

            // Evaluate unlocks (should happen automatically after RecordDiscoveryAsync)
            await grain.EvaluateUnlocksAsync();

            // Assert
            var isUnlocked = await grain.IsGeneratorUnlockedAsync("TestGenerator");
            Assert.That(isUnlocked, Is.True);
        }

        [Test]
        public async Task MetaProgressionGrain_EvaluateUnlocks_DoesNotUnlockWhenCriteriaNotMet()
        {
            // Arrange
            var playerId = "player-7";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Add criteria that requires 5 world visits
            var criteria = new UnlockCriteria
            {
                CriteriaId = "criteria-3",
                UnlocksGenerator = "UnlockedGenerator",
                MinWorldVisits = 5
            };

            await grain.AddUnlockCriteriaAsync(criteria);

            // Act - visit only 2 worlds (not enough)
            await grain.RecordDiscoveryAsync("world-1", "map-1");
            await grain.RecordDiscoveryAsync("world-2", "map-2");
            await grain.EvaluateUnlocksAsync();

            // Assert
            var isUnlocked = await grain.IsGeneratorUnlockedAsync("UnlockedGenerator");
            Assert.That(isUnlocked, Is.False);
        }

        [Test]
        public async Task MetaProgressionGrain_RecordDiscovery_TracksTagVisitCounts()
        {
            // Arrange
            var playerId = "player-8";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Act - visit worlds with same tag multiple times
            await grain.RecordDiscoveryAsync("world-1", "map-1", null, new List<string> { "dungeon" });
            await grain.RecordDiscoveryAsync("world-2", "map-2", null, new List<string> { "dungeon" });
            await grain.RecordDiscoveryAsync("world-3", "map-3", null, new List<string> { "dungeon" });

            // Assert
            var state = await grain.GetStateAsync();
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.TagVisitCounts.ContainsKey("dungeon"), Is.True);
            Assert.That(state.TagVisitCounts["dungeon"], Is.EqualTo(3));
        }

        [Test]
        public async Task MetaProgressionGrain_CrossWorldQuestUnlock_UnlocksAfterQuests()
        {
            // Arrange
            var playerId = "player-9";
            var grain = _cluster!.GrainFactory.GetGrain<IMetaProgressionGrain>(playerId);

            // Add criteria that requires cross-world quests
            var criteria = new UnlockCriteria
            {
                CriteriaId = "criteria-4",
                UnlocksGenerator = "CrossWorldGenerator",
                MinCrossWorldQuests = 2
            };

            await grain.AddUnlockCriteriaAsync(criteria);

            // Act - complete cross-world quests
            await grain.RecordQuestCompletionAsync("quest-1", isCrossWorld: true);
            await grain.RecordQuestCompletionAsync("quest-2", isCrossWorld: true);

            // Evaluate unlocks (should happen automatically)
            await grain.EvaluateUnlocksAsync();

            // Assert
            var isUnlocked = await grain.IsGeneratorUnlockedAsync("CrossWorldGenerator");
            Assert.That(isUnlocked, Is.True);
        }

        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                // Add in-memory grain storage
                siloBuilder.AddMemoryGrainStorage("metaStore");
            }
        }
    }
}

