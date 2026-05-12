using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Components;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.Test.TestStubs;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies that <c>GameMapGrain.FanOutAsync</c> and <c>SendToActorAsync</c> persist every
    /// stamped <see cref="MapDelta"/> to <see cref="IWorldSnapshotStore.AppendMapDeltaAsync"/>
    /// before fan-out completes, per the world-persistence delta-log-append requirement.
    /// </summary>
    [TestFixture]
    public class GameMapGrainPersistenceTests
    {
        // Captured by the silo configurator at deploy time so test methods can read
        // back what was appended. One instance per fixture (silo-scoped singleton).
        private static RecordingWorldSnapshotStore _recordingStore = null!;
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");

                siloBuilder.Configure<SiloMessagingOptions>(opts =>
                {
                    opts.ResponseTimeout = TimeSpan.FromMinutes(3);
                });

                siloBuilder.ConfigureServices(services =>
                {
                    services.Configure<Aetherium.Server.Simulation.SimulationOptions>(opts =>
                    {
                        opts.RegionSize = 128;
                        opts.EnableWeather = false;
                        opts.EnableSeasons = false;
                        opts.EnableAgentChanges = false;
                        opts.EnableProceduralEvents = false;
                    });

                    services.AddSingleton<IWorldSnapshotStore>(_recordingStore);

                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _recordingStore = new RecordingWorldSnapshotStore();
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void ResetRecorder() => _recordingStore.Reset();

        private async Task<(IGameMapGrain map, string worldId, string mapId, string playerA, WorldLocation spawnA)>
            InitMapWithPlayerAsync(string seed = "")
        {
            var worldId = $"world-{Guid.NewGuid()}{seed}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Persistence Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze",
                new Dictionary<string, object>());

            var playerA = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(playerA);
            Assert.That(join.Success, Is.True);
            return (map, worldId, mapId, playerA, join.SpawnLocation());
        }

        [Test]
        public async Task FanOutAsync_Persists_Every_Delta_With_World_And_Map_Keys()
        {
            var (_, worldId, mapId, _, _) = await InitMapWithPlayerAsync("-fan-out");

            // JoinPlayerAsync emits at least one fan-out delta (EntityAddedDelta for the new Character).
            var records = _recordingStore.RecordedFull;
            Assert.That(records, Is.Not.Empty, "JoinPlayer should produce at least one persisted delta");
            Assert.That(records.All(r => r.WorldId == worldId),
                $"All persisted deltas should carry WorldId={worldId}. Got: {string.Join(",", records.Select(r => r.WorldId))}");
            Assert.That(records.All(r => r.RegionId == mapId),
                $"All persisted deltas should carry RegionId={mapId} (the grain's MapId).");
        }

        [Test]
        public async Task FanOutAsync_Persists_Sequence_Monotonically()
        {
            var (map, _, _, playerA, _) = await InitMapWithPlayerAsync("-monotonic");

            // Drive several mutations to produce a multi-delta stream.
            await map.RotateAsync(playerA, 90);
            await map.RotateAsync(playerA, 180);
            await map.MoveAsync(playerA, Aetherium.Model.RelativeDirection.Forward, 1);

            var sequences = _recordingStore.Recorded.Select(d => d.Sequence).ToArray();
            Assert.That(sequences, Is.Not.Empty);

            // Per-grain sequence numbers must be strictly increasing.
            for (int i = 1; i < sequences.Length; i++)
            {
                Assert.That(sequences[i], Is.GreaterThan(sequences[i - 1]),
                    $"Sequence regressed at index {i}: {sequences[i - 1]} -> {sequences[i]}");
            }
        }

        [Test]
        public async Task SendToActorAsync_Heading_Change_Is_Persisted()
        {
            var (map, _, _, playerA, _) = await InitMapWithPlayerAsync("-heading");

            // RotateAsync routes through SendToActorAsync (heading is actor-only).
            await map.RotateAsync(playerA, 270);

            var headingDeltas = _recordingStore.Recorded.OfType<EntityHeadingChangedDelta>().ToArray();
            Assert.That(headingDeltas, Is.Not.Empty,
                "Rotate should produce a persisted EntityHeadingChangedDelta via SendToActorAsync");
            Assert.That(headingDeltas.Last().Degrees, Is.EqualTo(270));
            Assert.That(headingDeltas.Last().EntityId, Is.EqualTo(playerA));
        }

        [Test]
        public async Task Persisted_Delta_Roundtrip_Is_Queryable_By_Sequence()
        {
            var (_, worldId, mapId, playerA, _) = await InitMapWithPlayerAsync("-query");

            await map_RotateThriceHelper(playerA);
            var snapshot = _recordingStore.Recorded.ToArray();
            Assert.That(snapshot, Is.Not.Empty);

            // The recording store delegates to the in-memory implementation for queries,
            // so we can replay the log just as a real cold-start would.
            var since0 = await _recordingStore.GetMapDeltasSinceSequenceAsync(worldId, mapId, 0);
            Assert.That(since0.Length, Is.EqualTo(snapshot.Length),
                "Range query (sinceSeq=0) should return every delta that was appended");

            CollectionAssert.AreEqual(
                snapshot.Select(d => d.Sequence).ToArray(),
                since0.Select(d => d.Sequence).ToArray(),
                "Returned ordering must match the persisted sequence order");
        }

        // Mutation helper kept private so the test class compiles without exposing _cluster captures.
        private async Task map_RotateThriceHelper(string playerA)
        {
            // Resolve the map grain by id pattern used in the latest InitMap call.
            // We can't easily plumb the IGameMapGrain back; rotate via the recorded
            // EntityHeadingChangedDelta path requires re-resolving — instead, lean on
            // the fact that the recorder is global to the fixture: any mutation by
            // any player produces persisted deltas. We simply trigger via the recorded
            // playerA's grain — which is the most recent test's map grain.
            var ids = _recordingStore.RecordedFull.Select(r => r.RegionId).Distinct().ToArray();
            var latestMapId = ids.Last();
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(latestMapId);
            await map.RotateAsync(playerA, 45);
            await map.RotateAsync(playerA, 135);
            await map.RotateAsync(playerA, 225);
        }
    }
}
