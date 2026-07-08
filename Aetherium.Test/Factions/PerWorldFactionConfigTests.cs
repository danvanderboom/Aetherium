using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Model.Factions;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Factions
{
    /// <summary>
    /// Verifies "Per-World Faction Config" and "Faction Relations As Data"
    /// (openspec/changes/wire-factions-live/specs/factions/spec.md): a world's
    /// <see cref="FactionConfig"/> — via <see cref="WorldConfig"/> or a <see cref="CreateWorldRequest"/>
    /// — reaches every map the world creates, and the compiled landscape is observable.
    /// </summary>
    [TestFixture]
    public class PerWorldFactionConfigTests
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

        private static FactionConfig TestConfig() => new()
        {
            Factions = new List<FactionDefinition>
            {
                new() { Id = "town", Name = "Rivertown", Tags = new List<string> { "settlement" } },
                new() { Id = "cult", Name = "Cult of the Fang" },
                new() { Id = "empire", Name = "The Empire" },
                new() { Id = "vassal", Name = "Vassal March" },
            },
            Relations = new List<FactionRelationDefinition>
            {
                new() { FromFactionId = "town", ToFactionId = "cult", Disposition = FactionDispositionKind.War, Mutual = true },
                new() { FromFactionId = "vassal", ToFactionId = "empire", Disposition = FactionDispositionKind.Subordinate, Mutual = false },
            },
            Bands = new List<StandingBand>
            {
                new() { Id = "neutral", MinStanding = -100 },
                new() { Id = "friendly", MinStanding = 200 },
            },
        };

        private async Task AssertMapHasFactionsAsync(string mapId)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);

            var state = await map.GetFactionsAsync();
            Assert.That(state.Factions.Select(f => f.Id), Does.Contain("town"), $"Map {mapId} must carry the world's factions.");

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            var ledger = await map.GetReputationAsync(player);
            Assert.That(ledger.Reputations.Select(r => r.FactionId), Does.Contain("town"),
                "Joining character must carry a ledger seeded with the world's factions.");
        }

        [Test]
        public async Task WorldFactionConfig_ReachesEveryMapItCreates()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Faction World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
                FactionConfig = TestConfig(),
            };

            await grain.InitializeAsync(config);
            var secondMapId = await grain.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();
            var initialMapId = mapIds.First(id => id != secondMapId);

            await AssertMapHasFactionsAsync(initialMapId);
            await AssertMapHasFactionsAsync(secondMapId);
        }

        [Test]
        public async Task CreateWorldRequest_FactionConfig_ReachesTheCreatedMap()
        {
            var management = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = $"Faction World {Guid.NewGuid()}",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                FactionConfig = TestConfig(),
            };

            var worldId = await management.CreateWorldAsync(request);
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await worldGrain.GetMapIdsAsync();

            Assert.That(mapIds, Is.Not.Empty);
            await AssertMapHasFactionsAsync(mapIds.First());
        }

        [Test]
        public async Task GetFactionsAsync_ReportsFactionsRelationsAndBands()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>(), null, null, null, TestConfig());

            var state = await map.GetFactionsAsync();

            Assert.That(state.Factions.Select(f => f.Id),
                Is.EquivalentTo(new[] { "town", "cult", "empire", "vassal" }));

            // Mutual war expands to both directions; directed subordination stays one-way.
            Assert.That(state.Relations.Any(r => r.FromFactionId == "town" && r.ToFactionId == "cult" && r.Disposition == FactionDispositionKind.War), Is.True);
            Assert.That(state.Relations.Any(r => r.FromFactionId == "cult" && r.ToFactionId == "town" && r.Disposition == FactionDispositionKind.War), Is.True);
            Assert.That(state.Relations.Any(r => r.FromFactionId == "vassal" && r.ToFactionId == "empire" && r.Disposition == FactionDispositionKind.Subordinate), Is.True);
            Assert.That(state.Relations.Any(r => r.FromFactionId == "empire" && r.ToFactionId == "vassal"), Is.False,
                "A directed relation must not be mirrored in the report.");

            Assert.That(state.Bands.Select(b => b.Id), Is.EquivalentTo(new[] { "neutral", "friendly" }));
        }
    }
}
