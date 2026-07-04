using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
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

        private static QuestDefinition CollectQuest(string id, string itemType, int requiredCount) =>
            new QuestDefinition
            {
                QuestId = id,
                Title = id,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        ObjectiveId = $"{id}-collect",
                        Type = "collect",
                        Parameters = new Dictionary<string, object>
                        {
                            ["itemType"] = itemType,
                            ["requiredCount"] = requiredCount
                        }
                    }
                }
            };

        private static QuestDefinition KillQuest(string id, string target, int requiredCount) =>
            new QuestDefinition
            {
                QuestId = id,
                Title = id,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        ObjectiveId = $"{id}-kill",
                        Type = "kill",
                        Parameters = new Dictionary<string, object>
                        {
                            ["enemyType"] = target,
                            ["requiredCount"] = requiredCount
                        }
                    }
                }
            };

        private static QuestDefinition ReachLocationQuest(string id, string locationHint) =>
            new QuestDefinition
            {
                QuestId = id,
                Title = id,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        ObjectiveId = $"{id}-reach",
                        Type = "reach_location",
                        Parameters = new Dictionary<string, object>
                        {
                            ["locationHint"] = locationHint
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

        // ---- Slice 2: collect / kill / reach_location objectives ----

        [Test]
        public async Task CollectObjective_CompletesQuest_OnSingleMatchingItem()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(CollectQuest("gather", "GoldCoin", requiredCount: 1));
            Assert.That(await grain.StartQuestAsync("gather"), Is.True);

            await grain.RecordEventAsync("item_collected", new Dictionary<string, object>
            {
                ["itemId"] = "coin-1",
                ["itemType"] = "GoldCoin"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state!.CompletedQuestIds, Does.Contain("gather"));
            Assert.That(state.ActiveQuestIds, Does.Not.Contain("gather"));
        }

        [Test]
        public async Task CollectObjective_AccumulatesCount_UntilRequiredReached()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(CollectQuest("set", "Relic", requiredCount: 3));
            Assert.That(await grain.StartQuestAsync("set"), Is.True);

            // Two matching collections — still short of 3.
            for (int i = 0; i < 2; i++)
            {
                await grain.RecordEventAsync("item_collected", new Dictionary<string, object>
                {
                    ["itemType"] = "Relic"
                });
            }

            var mid = await grain.GetStateAsync();
            Assert.That(mid!.ActiveQuestIds, Does.Contain("set"), "Quest not yet complete after 2/3.");
            Assert.That(mid.CompletedQuestIds, Does.Not.Contain("set"));
            Assert.That(mid.ObjectiveProgress["set"]["set-collect"], Is.EqualTo(2), "Partial progress tracked.");

            // Third completes it.
            await grain.RecordEventAsync("item_collected", new Dictionary<string, object>
            {
                ["itemType"] = "Relic"
            });

            var done = await grain.GetStateAsync();
            Assert.That(done!.CompletedQuestIds, Does.Contain("set"));
            Assert.That(done.ActiveQuestIds, Does.Not.Contain("set"));
        }

        [Test]
        public async Task CollectObjective_IgnoresNonMatchingItemType()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(CollectQuest("gather", "GoldCoin", requiredCount: 1));
            Assert.That(await grain.StartQuestAsync("gather"), Is.True);

            await grain.RecordEventAsync("item_collected", new Dictionary<string, object>
            {
                ["itemType"] = "Rock"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state!.ActiveQuestIds, Does.Contain("gather"), "Wrong item type must not progress the quest.");
            Assert.That(state.CompletedQuestIds, Does.Not.Contain("gather"));
        }

        [Test]
        public async Task KillObjective_CompletesQuest_AfterRequiredKills()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(KillQuest("cull", "Wraith", requiredCount: 2));
            Assert.That(await grain.StartQuestAsync("cull"), Is.True);

            await grain.RecordEventAsync("enemy_defeated", new Dictionary<string, object> { ["enemyType"] = "Wraith" });
            var mid = await grain.GetStateAsync();
            Assert.That(mid!.ActiveQuestIds, Does.Contain("cull"), "One kill of two — still active.");

            await grain.RecordEventAsync("enemy_defeated", new Dictionary<string, object> { ["enemyType"] = "Wraith" });
            var done = await grain.GetStateAsync();
            Assert.That(done!.CompletedQuestIds, Does.Contain("cull"));
            Assert.That(done.ActiveQuestIds, Does.Not.Contain("cull"));
        }

        [Test]
        public async Task ReachLocationObjective_CompletesQuest_WhenHintMatchesArrival()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(ReachLocationQuest("explore", "dungeon"));
            Assert.That(await grain.StartQuestAsync("explore"), Is.True);

            // reach_location is driven by arrival; the hint fuzzy-matches the arrival's mapId.
            await grain.RecordEventAsync("player_arrived", new Dictionary<string, object>
            {
                ["worldId"] = "world-A",
                ["mapId"] = "sunless-dungeon-3"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state!.CompletedQuestIds, Does.Contain("explore"));
            Assert.That(state.ActiveQuestIds, Does.Not.Contain("explore"));
        }

        [Test]
        public async Task ReachLocationObjective_DoesNotComplete_OnUnrelatedArrival()
        {
            var grain = NewStateGrain();
            await grain.AddGeneratedQuestAsync(ReachLocationQuest("explore", "dungeon"));
            Assert.That(await grain.StartQuestAsync("explore"), Is.True);

            await grain.RecordEventAsync("player_arrived", new Dictionary<string, object>
            {
                ["worldId"] = "world-A",
                ["mapId"] = "sunny-meadow"
            });

            var state = await grain.GetStateAsync();
            Assert.That(state!.ActiveQuestIds, Does.Contain("explore"), "Hint should not match an unrelated map.");
            Assert.That(state.CompletedQuestIds, Does.Not.Contain("explore"));
        }
    }
}
