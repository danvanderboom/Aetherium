using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Aetherium.Server.Narrative;
using Aetherium.Server.Narrative.State;

namespace Aetherium.Test.Narrative
{
    /// <summary>
    /// Covers quest activation and the travel_to completion loop on NarrativeStateGrain — the P3-2
    /// gap: before this, ActiveQuestIds was never populated, so objectives could never progress.
    /// </summary>
    [TestFixture]
    public class NarrativeStateGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("narrativeStore");
                // MarkQuestCompletedAsync records to meta-progression (best-effort); register its
                // store so the completion path doesn't hit the missing-provider exception branch.
                siloBuilder.AddMemoryGrainStorage("metaStore");
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        private INarrativeStateGrain NewStateGrain() =>
            _cluster.GrainFactory.GetGrain<INarrativeStateGrain>($"state-{Guid.NewGuid()}");

        private static QuestDefinition SimpleQuest(string id, params string[] prerequisites) =>
            new QuestDefinition
            {
                QuestId = id,
                Title = id,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective { ObjectiveId = $"{id}-obj", Type = "kill",
                        Parameters = new Dictionary<string, object> { ["target"] = "Goblin" } }
                },
                PrerequisiteQuestIds = prerequisites.ToList()
            };

        private static QuestDefinition TravelQuest(string id, string worldId, string mapId) =>
            new QuestDefinition
            {
                QuestId = id,
                Title = id,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        ObjectiveId = $"{id}-travel",
                        Type = "travel_to",
                        Parameters = new Dictionary<string, object>
                        {
                            ["worldSelector"] = new Dictionary<string, object> { ["worldId"] = worldId },
                            ["mapSelector"] = new Dictionary<string, object> { ["mapId"] = mapId }
                        }
                    }
                }
            };

        [Test]
        public async Task StartQuest_ActivatesQuest_AndPopulatesActiveSet()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(SimpleQuest("q1"));

            var started = await grain.StartQuestAsync("q1");

            Assert.That(started, Is.True);
            var active = await grain.GetActiveQuestIdsAsync();
            Assert.That(active, Does.Contain("q1"));
        }

        [Test]
        public async Task StartQuest_UnknownQuest_ReturnsFalse()
        {
            var grain = NewStateGrain();
            Assert.That(await grain.StartQuestAsync("does-not-exist"), Is.False);
        }

        [Test]
        public async Task StartQuest_AlreadyActive_ReturnsFalse()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(SimpleQuest("q1"));

            Assert.That(await grain.StartQuestAsync("q1"), Is.True);
            Assert.That(await grain.StartQuestAsync("q1"), Is.False, "A quest already active cannot be started again.");
        }

        [Test]
        public async Task StartQuest_RespectsPrerequisites()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(SimpleQuest("prereq"));
            await grain.AddGeneratedQuestAsync(SimpleQuest("gated", "prereq"));

            // Prerequisite not completed → cannot start.
            Assert.That(await grain.StartQuestAsync("gated"), Is.False);

            // Complete the prerequisite, then the gated quest can start.
            await grain.MarkQuestCompletedAsync("prereq");
            Assert.That(await grain.StartQuestAsync("gated"), Is.True);
        }

        [Test]
        public async Task TravelToObjective_CompletesQuest_OnMatchingArrival()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(TravelQuest("journey", "world-A", "map-1"));

            Assert.That(await grain.StartQuestAsync("journey"), Is.True);

            // Player arrives at the objective's target world/map.
            await grain.RecordEventAsync("player_arrived", new Dictionary<string, object>
            {
                ["worldId"] = "world-A",
                ["mapId"] = "map-1",
                ["playerId"] = "p1"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.CompletedQuestIds, Does.Contain("journey"), "Quest should complete when its only objective is met.");
            Assert.That(state.ActiveQuestIds, Does.Not.Contain("journey"), "Completed quest should leave the active set.");
            Assert.That(state.CompletedObjectives.ContainsKey("journey"), Is.True);
            Assert.That(state.CompletedObjectives["journey"], Does.Contain("journey-travel"));
        }

        [Test]
        public async Task TravelToObjective_DoesNotComplete_OnWrongArrival()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(TravelQuest("journey", "world-A", "map-1"));
            Assert.That(await grain.StartQuestAsync("journey"), Is.True);

            // Arrive somewhere else — objective must not complete.
            await grain.RecordEventAsync("player_arrived", new Dictionary<string, object>
            {
                ["worldId"] = "world-B",
                ["mapId"] = "map-9"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state!.CompletedQuestIds, Does.Not.Contain("journey"));
            Assert.That(state.ActiveQuestIds, Does.Contain("journey"), "Quest should still be active after a non-matching arrival.");
        }
    }
}
