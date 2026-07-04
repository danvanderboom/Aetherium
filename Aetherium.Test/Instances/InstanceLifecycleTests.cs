using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Instances;
using Aetherium.Server.MultiWorld;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Test.Instances
{
    /// <summary>
    /// Covers the P3-4 instance-lifecycle fixes: releasing/sweeping an instance frees its map
    /// (previously leaked and ticked forever), and lockout recording is idempotent for the same
    /// instance (previously double-counted attempts and extended the window on every entry).
    /// This grain stack previously had zero tests.
    /// </summary>
    [TestFixture]
    public class InstanceLifecycleTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");

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

        // ---- helpers ----

        private async Task<(string worldId, IWorldGrain world)> CreateActiveWorldAsync()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Instance Test World",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            });
            return (worldId, world);
        }

        private async Task<(InstanceId instanceId, string mapId)> AllocateInstanceAsync(
            string worldId, string dungeonId, params string[] players)
        {
            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            var instanceId = await allocator.AllocateInstanceAsync(
                new DungeonId(dungeonId), null, players.Select(p => new PlayerId(p)).ToList());
            var instanceGrain = _cluster.GrainFactory.GetGrain<IDungeonInstanceGrain>(instanceId.Value);
            var mapId = await instanceGrain.GetMapIdAsync();
            Assert.That(mapId, Is.Not.Null.And.Not.Empty, "Allocated instance should have a map.");
            return (instanceId, mapId!);
        }

        // ---- RemoveMapAsync ----

        [Test]
        public async Task RemoveMapAsync_DropsMapFromWorld()
        {
            var (worldId, world) = await CreateActiveWorldAsync();
            var newMapId = await world.AddMapAsync("Annex", "maze", new Dictionary<string, object>());

            Assert.That(await world.GetMapIdsAsync(), Does.Contain(newMapId));

            var removed = await world.RemoveMapAsync(newMapId);

            Assert.That(removed, Is.True);
            Assert.That(await world.GetMapIdsAsync(), Does.Not.Contain(newMapId),
                "Removed map must no longer be ticked/enumerated.");
        }

        [Test]
        public async Task RemoveMapAsync_ReturnsFalse_ForUnknownMap()
        {
            var (_, world) = await CreateActiveWorldAsync();
            Assert.That(await world.RemoveMapAsync("no-such-map"), Is.False);
        }

        // ---- ReleaseInstanceAsync frees the map ----

        [Test]
        public async Task ReleaseInstanceAsync_RemovesInstanceMapFromWorld()
        {
            var (worldId, world) = await CreateActiveWorldAsync();
            var (instanceId, mapId) = await AllocateInstanceAsync(worldId, "dungeon-1", "p1");

            Assert.That(await world.GetMapIdsAsync(), Does.Contain(mapId));

            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            await allocator.ReleaseInstanceAsync(instanceId);

            Assert.That(await world.GetMapIdsAsync(), Does.Not.Contain(mapId),
                "Releasing an instance must free its map so it stops being ticked.");
        }

        // ---- Sweeper ----

        [Test]
        public async Task Sweeper_ReapsAbandonedInstance_AndFreesMap()
        {
            var (worldId, world) = await CreateActiveWorldAsync();
            var (instanceId, mapId) = await AllocateInstanceAsync(worldId, "dungeon-1", "p1");

            // Emptying the instance marks it Abandoned.
            var instanceGrain = _cluster.GrainFactory.GetGrain<IDungeonInstanceGrain>(instanceId.Value);
            await instanceGrain.RemovePlayerAsync(new PlayerId("p1"));
            var info = await instanceGrain.GetInfoAsync();
            Assert.That(info!.State, Is.EqualTo(InstanceState.Abandoned));

            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            var reaped = await allocator.SweepAbandonedInstancesAsync();

            Assert.That(reaped, Is.EqualTo(1), "The abandoned instance should be reaped.");
            Assert.That(await world.GetMapIdsAsync(), Does.Not.Contain(mapId),
                "Sweeping should free the reaped instance's map.");
        }

        [Test]
        public async Task Sweeper_LeavesActiveInstanceAlone()
        {
            var (worldId, world) = await CreateActiveWorldAsync();
            var (instanceId, mapId) = await AllocateInstanceAsync(worldId, "dungeon-1", "p1");

            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            var reaped = await allocator.SweepAbandonedInstancesAsync();

            Assert.That(reaped, Is.EqualTo(0), "An active, populated instance must not be reaped.");
            Assert.That(await world.GetMapIdsAsync(), Does.Contain(mapId));
        }

        // ---- Lockout idempotence ----

        [Test]
        public async Task RecordLockout_IsIdempotent_ForSameInstance_ButCountsNewInstances()
        {
            var dungeonId = $"lockdungeon-{Guid.NewGuid()}";
            var ledger = _cluster.GrainFactory.GetGrain<ILockoutLedgerGrain>(dungeonId);
            await ledger.SetPolicyAsync(new LockoutPolicy
            {
                DungeonId = new DungeonId(dungeonId),
                Type = LockoutType.TimeBased,
                Duration = TimeSpan.FromHours(1),
                MaxAttempts = -1,
                ResetOnSuccess = false
            });

            var player = new PlayerId("p1");
            var players = new List<PlayerId> { player };
            var instanceA = new InstanceId("instance-A");

            await ledger.RecordLockoutAsync(null, players, instanceA);
            var first = (await ledger.GetPlayerLockoutsAsync(player)).Single();
            Assert.That(first.AttemptsUsed, Is.EqualTo(1));
            var firstUntil = first.LockoutUntil;

            // Re-entering the SAME instance must not increment attempts or extend the window.
            await ledger.RecordLockoutAsync(null, players, instanceA);
            var same = (await ledger.GetPlayerLockoutsAsync(player)).Single();
            Assert.That(same.AttemptsUsed, Is.EqualTo(1), "Re-entering the same instance must not double-count.");
            Assert.That(same.LockoutUntil, Is.EqualTo(firstUntil), "Re-entering the same instance must not extend the window.");

            // A DIFFERENT instance is a genuine new run: attempts increment.
            await ledger.RecordLockoutAsync(null, players, new InstanceId("instance-B"));
            var afterNew = (await ledger.GetPlayerLockoutsAsync(player)).Single();
            Assert.That(afterNew.AttemptsUsed, Is.EqualTo(2), "A distinct instance entry should count.");
        }
    }
}
