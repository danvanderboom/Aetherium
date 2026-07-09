using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Model.Progression;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Progression
{
    /// <summary>
    /// Verifies "Per-World Progression Config" (openspec/changes/wire-progression-live): a world's
    /// <see cref="ProgressionConfig"/> — via <see cref="WorldConfig"/> or a <see cref="CreateWorldRequest"/>
    /// — reaches every map the world creates, so progression is per-world data, not engine-hardcoded.
    /// </summary>
    [TestFixture]
    public class PerWorldProgressionConfigTests
    {
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
                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore,
                        Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();
                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });
                    services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<Aetherium.Server.GameHub>>(
                        new Aetherium.Test.MultiWorld.CapturingHubContext());
                    services.AddSingleton<Aetherium.Server.GameSessionManager>();
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
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        private static ProgressionConfig ConfigWithCombatPool() => new()
        {
            Pools = new List<ProgressPoolDefinition>
            {
                new() { Id = "combat", Curve = new LevelCurveDefinition { XpPerLevel = 100 }, StartingLevel = 1 },
            },
            StartingAttributes = new Dictionary<string, double> { ["vitality"] = 110 },
        };

        private async Task AssertMapHasProgressionAsync(string mapId)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True, $"Could not join a player on map {mapId}.");

            var prog = await map.GetProgressionAsync(player);
            Assert.That(prog.Pools.Any(p => p.Id == "combat"), Is.True, "Joining character must carry the world's configured combat pool.");
            Assert.That(prog.Attributes.ContainsKey("vitality"), Is.True, "Joining character must carry the world's starting attributes.");
        }

        [Test]
        public async Task WorldProgressionConfig_ReachesEveryMapItCreates()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Progression World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
                ProgressionConfig = ConfigWithCombatPool(),
            };

            await grain.InitializeAsync(config);
            var secondMapId = await grain.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();
            var initialMapId = mapIds.First(id => id != secondMapId);

            await AssertMapHasProgressionAsync(initialMapId);
            await AssertMapHasProgressionAsync(secondMapId);
        }

        [Test]
        public async Task CreateWorldRequest_ProgressionConfig_ReachesTheCreatedMap()
        {
            var management = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = $"Progression World {Guid.NewGuid()}",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                ProgressionConfig = ConfigWithCombatPool(),
            };

            var worldId = await management.CreateWorldAsync(request);
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await worldGrain.GetMapIdsAsync();

            Assert.That(mapIds, Is.Not.Empty);
            await AssertMapHasProgressionAsync(mapIds.First());
        }

        [Test]
        public async Task NoProgressionConfig_NoComponentsStamped()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>()); // no progression config

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);

            var prog = await map.GetProgressionAsync(player);
            Assert.That(prog.Pools, Is.Empty, "A world with no progression config stamps no pools.");
            Assert.That(prog.Attributes, Is.Empty);
            Assert.That(prog.UnlockedSkills, Is.Empty);
        }
    }
}
