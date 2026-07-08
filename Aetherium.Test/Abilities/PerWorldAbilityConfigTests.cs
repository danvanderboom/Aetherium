using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Model.Abilities;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Abilities
{
    /// <summary>
    /// Verifies "Per-World Ability Config" (openspec/changes/wire-abilities-live/specs/abilities/spec.md):
    /// a world's <see cref="AbilityConfig"/> — supplied via <see cref="WorldConfig"/> or a
    /// <see cref="CreateWorldRequest"/> — reaches every map the world creates, so the abilities a
    /// player can cast and the pools they carry are per-world data, not engine-hardcoded.
    /// </summary>
    [TestFixture]
    public class PerWorldAbilityConfigTests
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

        private static AbilityConfig ConfigWithFocusAbility() => new()
        {
            CharacterResourcePools = new List<ResourcePoolDefinition>
            {
                new() { Tag = "energy", Max = 100, StartingValue = 40 },
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "focus", Effects = { new() { Kind = AbilityEffectKind.ModifyResource, PoolTag = "energy", Delta = -5, ResourceTarget = AbilityEffectTarget.Caster } } },
            },
        };

        /// <summary>Asserts a map received the ability config: a joining player carries the "energy"
        /// pool and can cast the config's "focus" ability.</summary>
        private async Task AssertMapHasAbilityConfigAsync(string mapId)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True, $"Could not join a player on map {mapId}.");

            var pools = await map.GetResourcePoolsAsync(player);
            Assert.That(pools.Pools.Any(p => p.Tag == "energy"), Is.True, "Joining character must carry the world's configured energy pool.");

            var cast = await map.UseAbilityAsync(player, "focus", null);
            Assert.That(cast.Success, Is.True, $"The world's 'focus' ability must be castable on map {mapId}: {cast.Reason}");
        }

        [Test]
        public async Task WorldAbilityConfig_ReachesEveryMapItCreates()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Ability World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
                AbilityConfig = ConfigWithFocusAbility(),
            };

            await grain.InitializeAsync(config); // creates the "Main" map
            var secondMapId = await grain.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();
            var initialMapId = mapIds.First(id => id != secondMapId);

            // The config must reach BOTH the inline-created map and one added later.
            await AssertMapHasAbilityConfigAsync(initialMapId);
            await AssertMapHasAbilityConfigAsync(secondMapId);
        }

        [Test]
        public async Task CreateWorldRequest_AbilityConfig_ReachesTheCreatedMap()
        {
            var management = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = $"Ability World {Guid.NewGuid()}",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                AbilityConfig = ConfigWithFocusAbility(),
            };

            var worldId = await management.CreateWorldAsync(request);
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await worldGrain.GetMapIdsAsync();

            Assert.That(mapIds, Is.Not.Empty);
            await AssertMapHasAbilityConfigAsync(mapIds.First());
        }
    }
}
