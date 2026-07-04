using System;
using System.Collections.Generic;
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
    /// P3-8b: delta-append failures must be observable (recorded, not silently swallowed), and once
    /// persistence recovers the grain must force a healing snapshot. Uses a fault-injecting store.
    /// </summary>
    [TestFixture]
    public class PersistenceHealthTests
    {
        private static FaultInjectingWorldSnapshotStore _store = null!;
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");
                siloBuilder.Configure<SiloMessagingOptions>(opts => opts.ResponseTimeout = TimeSpan.FromMinutes(3));
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
            _store = new FaultInjectingWorldSnapshotStore();
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void ResetStore() => _store.FailAppends = false;

        private async Task<IGameMapGrain> InitMapWithPlayerAsync(string seed)
        {
            var worldId = $"world-{Guid.NewGuid()}{seed}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Health Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 }, "maze", new Dictionary<string, object>());
            var join = await map.JoinPlayerAsync($"player-{Guid.NewGuid()}");
            Assert.That(join.Success, Is.True);
            return map;
        }

        [Test]
        public async Task AppendFailure_IsRecorded_NotSwallowed()
        {
            var map = await InitMapWithPlayerAsync("-fail");

            var healthy = await map.GetPersistenceHealthAsync();
            Assert.That(healthy.Healthy, Is.True);
            Assert.That(healthy.DeltaAppendFailureCount, Is.EqualTo(0));

            // Cause deltas to be emitted while the store rejects appends. Joining a second player
            // reliably fans out at least one delta; the grain must not stall on the failure.
            _store.FailAppends = true;
            var join = await map.JoinPlayerAsync($"player-{Guid.NewGuid()}");
            Assert.That(join.Success, Is.True, "gameplay must continue despite persistence failure");

            var degraded = await map.GetPersistenceHealthAsync();
            Assert.That(degraded.DeltaAppendFailureCount, Is.GreaterThan(0), "the append failure must be counted, not swallowed");
            Assert.That(degraded.Healthy, Is.False);
            Assert.That(degraded.LastError, Is.Not.Null.And.Not.Empty);
            Assert.That(degraded.LastFailureAtUtc, Is.Not.Null);
        }

        [Test]
        public async Task Recovery_ForcesHealSnapshot()
        {
            var map = await InitMapWithPlayerAsync("-heal");

            // Fail some appends so persistence is marked dirty.
            _store.FailAppends = true;
            await map.JoinPlayerAsync($"player-{Guid.NewGuid()}");
            var degraded = await map.GetPersistenceHealthAsync();
            Assert.That(degraded.Healthy, Is.False);

            var snapshotsBefore = _store.SaveSnapshotCount;

            // Recover: the next successful append should clear dirty and force a healing snapshot.
            _store.FailAppends = false;
            await map.JoinPlayerAsync($"player-{Guid.NewGuid()}");

            var recovered = await map.GetPersistenceHealthAsync();
            Assert.That(recovered.Healthy, Is.True, "persistence should report healthy once appends succeed again");

            // The heal snapshot is fire-and-forget on the grain scheduler; poll briefly for it.
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (_store.SaveSnapshotCount <= snapshotsBefore && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            Assert.That(_store.SaveSnapshotCount, Is.GreaterThan(snapshotsBefore),
                "recovery should force a full snapshot to supersede the deltas that failed to append");
        }
    }
}
