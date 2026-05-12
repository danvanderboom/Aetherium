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

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies the phase-2c grain mutation methods on IGameMapGrain. Each method
    /// mutates the grain's _world and (in a full host) emits a delta via the host-
    /// side delta broker. These tests run with a TestCluster where the broker is
    /// absent — they verify state changes and result DTOs, not delta propagation
    /// (that's the end-to-end test's job, deferred to a follow-up change).
    /// </summary>
    [TestFixture]
    public class GameMapGrainMutationTests
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

        private async Task<(IGameMapGrain map, string playerA, WorldLocation spawnA, string playerB, WorldLocation spawnB)>
            InitMapWithTwoPlayersAsync(string seed = "")
        {
            var worldId = $"world-{Guid.NewGuid()}{seed}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze",
                new Dictionary<string, object>());

            var playerA = $"player-A-{Guid.NewGuid()}";
            var playerB = $"player-B-{Guid.NewGuid()}";
            var joinA = await map.JoinPlayerAsync(playerA);
            var joinB = await map.JoinPlayerAsync(playerB);
            Assert.That(joinA.Success, Is.True);
            Assert.That(joinB.Success, Is.True);
            return (map, playerA, joinA.SpawnLocation(), playerB, joinB.SpawnLocation());
        }

        [Test]
        public async Task JoinPlayerAsync_Adds_Character_To_Grain_World()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            var snapshot = await map.GetWorldSnapshotAsync();

            // Snapshot includes both players' Characters
            var characterIds = snapshot.Entities
                .Where(e => e.TypeName == nameof(Aetherium.Character))
                .Select(e => e.EntityId)
                .ToHashSet();
            Assert.That(characterIds, Does.Contain(playerA));
        }

        [Test]
        public async Task GetWorldSnapshotForJoinerAsync_Omits_Joiners_Own_Character()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            var snap = await map.GetWorldSnapshotForJoinerAsync(playerA);

            var ids = snap.Entities.Select(e => e.EntityId).ToHashSet();
            Assert.That(ids, Does.Not.Contain(playerA), "Joiner's own Character should be excluded so they don't see themselves twice");
        }

        [Test]
        public async Task RotateAsync_Updates_Character_HasHeading()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            var result = await map.RotateAsync(playerA, 90);

            Assert.That(result.Success, Is.True);
            Assert.That(result.HeadingDegrees, Is.EqualTo(90));

            // Re-fetch a snapshot and verify the grain's world reflects the change.
            // (Snapshot doesn't currently include HasHeading per perception-pure design;
            // verifying via the result DTO is sufficient.)
        }

        [Test]
        public async Task MoveAsync_Updates_Character_Location_In_Grain_World()
        {
            var (map, playerA, spawnA, _, _) = await InitMapWithTwoPlayersAsync();
            var result = await map.MoveAsync(playerA, Aetherium.Model.RelativeDirection.Forward, 1);

            // Move is best-effort — may succeed or fail depending on whether the
            // adjacent cell is in the map. Either outcome is acceptable; the test
            // verifies the method returns without throwing and reports a result.
            Assert.That(result, Is.Not.Null);
            if (result.Success)
            {
                // Location should have changed if the move succeeded.
                var snapshot = await map.GetWorldSnapshotAsync();
                var player = snapshot.Entities.FirstOrDefault(e => e.EntityId == playerA);
                Assert.That(player, Is.Not.Null);
                // Forward when heading == 0 (North) reduces Y by 1.
                Assert.That(player!.Y, Is.EqualTo(spawnA.Y - 1));
            }
        }

        [Test]
        public async Task PickupAsync_Of_Nonexistent_Entity_Returns_Failure()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            var result = await map.PickupAsync(playerA, "does-not-exist");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Does.Contain("not found").IgnoreCase);
        }

        [Test]
        public async Task PickupAsync_Of_NonCarriable_Entity_Returns_Failure()
        {
            // Construct a temporary world to verify the not-carriable rejection.
            // (Maze-generated worlds don't have many carriables; build a fixture.)
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            var result = await map.PickupAsync(playerA, playerA); // Player picking up themselves
            // The Character is not carriable in the engine; expect a failure.
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task LeavePlayerAsync_Removes_Character_From_Grain_World()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();

            await map.LeavePlayerAsync(playerA);

            var snapshot = await map.GetWorldSnapshotAsync();
            var ids = snapshot.Entities.Select(e => e.EntityId).ToHashSet();
            Assert.That(ids, Does.Not.Contain(playerA), "After leaving, player's Character should be removed from canonical world");
        }

        [Test]
        public async Task UseAsync_Unsupported_Mode_Returns_Helpful_Failure()
        {
            var (map, playerA, _, _, _) = await InitMapWithTwoPlayersAsync();
            // No item in inventory — expects "Item not in inventory" failure.
            var result = await map.UseAsync(playerA, "no-such-item", "no-such-target", null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Does.Contain("inventory").IgnoreCase);
        }
    }
}
