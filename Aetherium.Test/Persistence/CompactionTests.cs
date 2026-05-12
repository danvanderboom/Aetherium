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

namespace Aetherium.Test.Persistence
{
    /// <summary>
    /// Verifies the Phase E compaction triggers: every appended delta past the configured
    /// threshold drives a snapshot capture (which compacts the log behind it), and the
    /// snapshot store reflects the trimmed state.
    /// </summary>
    [TestFixture]
    public class CompactionTests
    {
        private static RecordingWorldSnapshotStore _store = null!;
        private TestCluster _cluster = null!;

        // Low threshold + disabled timer (large interval) → tests deterministically
        // observe the threshold trigger.
        private const int DeltaThreshold = 3;

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

                    services.Configure<PersistenceOptions>(opts =>
                    {
                        opts.Compaction.Enabled = true;
                        opts.Compaction.DeltaCountThreshold = DeltaThreshold;
                        // High enough that the periodic timer never fires during a test.
                        opts.Compaction.IntervalMinutes = 60;
                    });

                    services.AddSingleton<IWorldSnapshotStore>(_store);

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
            _store = new RecordingWorldSnapshotStore();
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void Reset() => _store.Reset();

        private async Task<(IGameMapGrain map, string worldId, string mapId, string playerA)> InitMapAsync(string seed)
        {
            var worldId = $"world-compact{seed}-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Compaction Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze",
                new Dictionary<string, object>());

            var playerA = $"player-A-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(playerA);
            Assert.That(join.Success, Is.True);
            return (map, worldId, mapId, playerA);
        }

        [Test]
        public async Task Threshold_Crossed_Triggers_Automatic_Snapshot()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-threshold");

            // Drive several mutations. With DeltaThreshold=3 and join already producing
            // some deltas, the very first follow-up rotates should trip the threshold.
            // Use enough rotations to be well past the threshold.
            for (int i = 0; i < 10; i++)
                await map.RotateAsync(playerA, (i * 30) % 360);

            // The compaction trigger fires asynchronously via fire-and-forget; give the
            // grain a moment to drain. We assert by polling rather than fixed sleep.
            RegionStateSnapshot? snapshot = null;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                snapshot = await _store.LoadSnapshotAsync(worldId, mapId);
                if (snapshot is not null) break;
                await Task.Delay(100);
            }

            Assert.That(snapshot, Is.Not.Null,
                "Automatic snapshot should land in the store once the delta threshold is crossed.");
            Assert.That(snapshot!.LastSequence, Is.GreaterThan(0),
                "Snapshot LastSequence should reflect the high-water mark at capture time.");
        }

        [Test]
        public async Task Threshold_Compaction_Drops_Log_Entries_At_Or_Below_Snapshot_Sequence()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-trim");

            // Generate enough mutations to trigger threshold compaction.
            for (int i = 0; i < 8; i++)
                await map.RotateAsync(playerA, (i * 45) % 360);

            // Poll for the snapshot to land.
            RegionStateSnapshot? snapshot = null;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                snapshot = await _store.LoadSnapshotAsync(worldId, mapId);
                if (snapshot is not null) break;
                await Task.Delay(100);
            }
            Assert.That(snapshot, Is.Not.Null);

            // Every retained log entry must have Sequence > snapshot.LastSequence.
            var remaining = await _store.GetMapDeltasSinceSequenceAsync(worldId, mapId, 0);
            Assert.That(remaining.All(d => d.Sequence > snapshot!.LastSequence), Is.True,
                $"After compaction, retained deltas should all have Sequence > {snapshot!.LastSequence}. " +
                $"Found sequences: {string.Join(",", remaining.Select(d => d.Sequence))}");
        }

        [Test]
        public async Task Compaction_Resets_Counter_So_Next_Cycle_Requires_Fresh_Deltas()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-counter-reset");

            // First cycle: cross the threshold.
            for (int i = 0; i < 6; i++)
                await map.RotateAsync(playerA, i * 60);

            RegionStateSnapshot? first = null;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                first = await _store.LoadSnapshotAsync(worldId, mapId);
                if (first is not null) break;
                await Task.Delay(100);
            }
            Assert.That(first, Is.Not.Null);
            var firstSeq = first!.LastSequence;

            // Drive one more mutation — below the threshold of 3. Should NOT immediately
            // trigger another snapshot.
            await map.RotateAsync(playerA, 999 % 360);
            await Task.Delay(200);

            var afterOne = await _store.LoadSnapshotAsync(worldId, mapId);
            Assert.That(afterOne!.LastSequence, Is.EqualTo(firstSeq),
                "A single delta after compaction must not produce a new snapshot (counter should have reset to 0).");

            // Drive enough more to cross threshold again. New snapshot should appear.
            for (int i = 0; i < 5; i++)
                await map.RotateAsync(playerA, (i * 71) % 360);

            for (int attempt = 0; attempt < 30; attempt++)
            {
                var current = await _store.LoadSnapshotAsync(worldId, mapId);
                if (current!.LastSequence > firstSeq) return; // success
                await Task.Delay(100);
            }
            Assert.Fail($"After {firstSeq} sequence, the second compaction cycle did not produce a fresh snapshot.");
        }
    }
}
