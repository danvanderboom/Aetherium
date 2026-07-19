using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Economy
{
    /// <summary>
    /// Verifies "Per-World Starting Currency" (add-starting-currency-data): a world's opening purse —
    /// supplied via <see cref="WorldConfig"/> or a <see cref="CreateWorldRequest"/> — reaches every map
    /// the world creates, so the credits a joining player's <c>Wallet</c> carries are per-world data, not
    /// the engine-hardcoded <c>Wallet.StartingCurrency</c> constant. Mirrors PerWorldAbilityConfigTests.
    /// </summary>
    [TestFixture]
    public class PerWorldStartingCurrencyTests
    {
        private const double CustomPurse = 1234.0;

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

                    // The GameManagementGrain (used by the CreateWorldRequest path) resolves these on
                    // activation; a capturing hub context is a no-op sink here.
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

        /// <summary>Joins a fresh player on the map and returns the credits their Wallet started with.</summary>
        private async Task<double> JoinAndReadPurseAsync(string mapId)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True, $"Could not join a player on map {mapId}.");
            return await map.GetWalletAsync(player);
        }

        [Test]
        public async Task WorldStartingCurrency_ReachesEveryMapItCreates()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Purse World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
                StartingCurrency = CustomPurse,
            };

            await grain.InitializeAsync(config); // creates the "Main" map
            var secondMapId = await grain.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();
            var initialMapId = mapIds.First(id => id != secondMapId);

            // The purse must reach BOTH the inline-created map and one added later.
            Assert.That(await JoinAndReadPurseAsync(initialMapId), Is.EqualTo(CustomPurse).Within(1e-9));
            Assert.That(await JoinAndReadPurseAsync(secondMapId), Is.EqualTo(CustomPurse).Within(1e-9));
        }

        [Test]
        public async Task CreateWorldRequest_StartingCurrency_ReachesTheCreatedMap()
        {
            var management = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = $"Purse World {Guid.NewGuid()}",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                StartingCurrency = CustomPurse,
            };

            var worldId = await management.CreateWorldAsync(request);
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await worldGrain.GetMapIdsAsync();

            Assert.That(mapIds, Is.Not.Empty);
            Assert.That(await JoinAndReadPurseAsync(mapIds.First()), Is.EqualTo(CustomPurse).Within(1e-9));
        }

        [Test]
        public async Task NoStartingCurrencySpecified_FallsBackToEngineDefault()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Default Purse World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
                // StartingCurrency intentionally left null.
            };

            await grain.InitializeAsync(config);
            var mapIds = await grain.GetMapIdsAsync();

            Assert.That(await JoinAndReadPurseAsync(mapIds.First()),
                Is.EqualTo(Aetherium.Components.Wallet.StartingCurrency).Within(1e-9));
        }
    }
}
