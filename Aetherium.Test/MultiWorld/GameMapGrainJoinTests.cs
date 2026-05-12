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
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies the phase-1 hub-grain bridge contract on the grain side:
    /// JoinPlayerAsync hands out distinct spawns, GetWorldSnapshotAsync produces
    /// snapshots whose entity IDs match the canonical world, and duplicate joiners
    /// are rejected cleanly.
    /// </summary>
    [TestFixture]
    public class GameMapGrainJoinTests
    {
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

                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore,
                        Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

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
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        private async Task<IGameMapGrain> InitMapAsync(string suffix = "")
        {
            var worldId = $"world-{Guid.NewGuid()}{suffix}";
            var mapId = $"{worldId}-map-1";

            // World grain doesn't strictly need to be initialized for the map grain
            // tests, but GameMapGrain.InitializeAsync calls back to it to look up
            // ClusterId. Initialize a minimal world so that lookup succeeds.
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await mapGrain.InitializeAsync(
                worldId,
                "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze",
                new Dictionary<string, object>());
            return mapGrain;
        }

        [Test]
        public async Task JoinPlayer_AssignsDistinctSpawns_ToTwoJoiners()
        {
            var mapGrain = await InitMapAsync();

            var a = await mapGrain.JoinPlayerAsync("player-a");
            var b = await mapGrain.JoinPlayerAsync("player-b");

            Assert.That(a.Success, Is.True, a.Reason);
            Assert.That(b.Success, Is.True, b.Reason);
            Assert.That(a.PlayerEntityId, Is.EqualTo("player-a"));
            Assert.That(b.PlayerEntityId, Is.EqualTo("player-b"));

            // Different cells.
            Assert.That(a.SpawnX != b.SpawnX || a.SpawnY != b.SpawnY || a.SpawnZ != b.SpawnZ,
                "Two joiners must get distinct spawn locations");
        }

        [Test]
        public async Task JoinPlayer_RejectsDuplicatePlayerId()
        {
            var mapGrain = await InitMapAsync();
            var first = await mapGrain.JoinPlayerAsync("dupe");
            Assert.That(first.Success, Is.True);

            var second = await mapGrain.JoinPlayerAsync("dupe");
            Assert.That(second.Success, Is.False);
            Assert.That(second.Reason, Does.Contain("already").IgnoreCase);
        }

        [Test]
        public async Task GetWorldSnapshot_ProducesIdenticalEntityIds_AcrossCalls()
        {
            var mapGrain = await InitMapAsync();

            var s1 = await mapGrain.GetWorldSnapshotAsync();
            var s2 = await mapGrain.GetWorldSnapshotAsync();

            // Same snapshot version
            Assert.That(s2.SnapshotVersion, Is.EqualTo(s1.SnapshotVersion));

            // Same entity ID set (order may differ).
            var ids1 = s1.Entities.Select(e => e.EntityId).OrderBy(x => x).ToList();
            var ids2 = s2.Entities.Select(e => e.EntityId).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(ids1, ids2);

            // Same recipe (so two joiners' SnapshotWorldBuilder would regenerate identical terrain).
            Assert.That(s2.Recipe.Seed, Is.EqualTo(s1.Recipe.Seed));
            Assert.That(s2.Recipe.GeneratorType, Is.EqualTo(s1.Recipe.GeneratorType));
            Assert.That(s2.Recipe.Width, Is.EqualTo(s1.Recipe.Width));
            Assert.That(s2.Recipe.Height, Is.EqualTo(s1.Recipe.Height));
        }

        [Test]
        public async Task JoinPlayer_OnUninitializedMap_ReturnsFailure()
        {
            // Fresh map grain that was never InitializeAsync'd.
            var orphan = _cluster.GrainFactory.GetGrain<IGameMapGrain>($"orphan-{Guid.NewGuid()}");
            var result = await orphan.JoinPlayerAsync("p");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Does.Contain("not initialized").IgnoreCase);
        }
    }
}
