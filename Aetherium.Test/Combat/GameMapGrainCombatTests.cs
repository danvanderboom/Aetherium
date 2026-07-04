using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Components;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Integration coverage of grain-authoritative combat: attacking on a live map applies to the
    /// grain's canonical world (damage accumulates, the target is removed on death). Verifies death
    /// through the public API — a follow-up attack on a defeated target reports "not found".
    /// </summary>
    [TestFixture]
    public class GameMapGrainCombatTests
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

        private async Task<(IGameMapGrain map, string player, string monsterId)> InitMapWithAdjacentMonsterAsync()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Combat Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>());

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            var spawn = join.SpawnLocation();

            // Spawn a monster on the first passable cardinal neighbour of the player's spawn, so it
            // is within attack reach (distance 1). At least one neighbour is passable (the spawn is
            // reachable), so this succeeds on any generated map.
            string? monsterId = null;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var spawnResult = await map.SpawnEntityAsync(new SpawnEntityRequest
                {
                    CreatureType = "monster",
                    X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z
                });
                if (spawnResult.Success)
                {
                    monsterId = spawnResult.EntityId;
                    break;
                }
            }

            Assert.That(monsterId, Is.Not.Null, "Expected at least one passable neighbour to place the monster.");
            return (map, player, monsterId!);
        }

        [Test]
        public async Task Attack_DamagesThenDefeats_MonsterOnLiveMap()
        {
            var (map, player, monsterId) = await InitMapWithAdjacentMonsterAsync();

            // Monsters spawn with 30 HP; 10 damage per hit → two non-lethal hits, third defeats.
            var r1 = await map.AttackAsync(player, monsterId);
            Assert.That(r1.Success, Is.True, r1.Reason);
            Assert.That(r1.Damage, Is.EqualTo(10));
            Assert.That(r1.RemainingHealth, Is.EqualTo(20));
            Assert.That(r1.TargetDefeated, Is.False);
            Assert.That(r1.TargetType, Is.EqualTo(nameof(Aetherium.Monster)));

            var r2 = await map.AttackAsync(player, monsterId);
            Assert.That(r2.RemainingHealth, Is.EqualTo(10));
            Assert.That(r2.TargetDefeated, Is.False);

            var r3 = await map.AttackAsync(player, monsterId);
            Assert.That(r3.RemainingHealth, Is.EqualTo(0));
            Assert.That(r3.TargetDefeated, Is.True, "Third hit should defeat the monster.");

            // The monster is gone from canonical state: attacking it again can't find it.
            var r4 = await map.AttackAsync(player, monsterId);
            Assert.That(r4.Success, Is.False, "A defeated target should no longer exist in the world.");
        }
    }
}
