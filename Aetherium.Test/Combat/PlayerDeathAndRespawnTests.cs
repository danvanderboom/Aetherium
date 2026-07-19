using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Components;
using Aetherium.Model.Combat;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Integration coverage of the live player death/respawn loop (engine gap-analysis §4.11,
    /// Phase 2 — see openspec/changes/wire-death-respawn-live Slice B). Drives a player to 0 HP via
    /// real monster retaliation on a live map and observes each of the four DeathPolicy outcome
    /// models (Permadeath × DownStateEnabled) through the public grain API.
    /// </summary>
    [TestFixture]
    public class PlayerDeathAndRespawnTests
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

        private async Task<(IGameMapGrain map, string player)> InitMapWithAdjacentMonsterAsync(
            DeathPolicy? deathPolicy, int? mapSeed = null)
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";

            // The map under test is created directly (like GameMapGrainCombatTests' helper) rather
            // than via WorldGrain.AddMapAsync, so the policy must be passed to InitializeAsync
            // itself — a WorldConfig.DeathPolicy would only reach a *different*, unused "Main" map
            // WorldGrain.InitializeAsync creates internally.
            //
            // A mapSeed pins the generated maze so a test that depends on a specific cell being
            // passable (see RespawnLocation_FixedCoordinates_*) is deterministic; without it the
            // maze is seeded randomly per run.
            var parameters = new Dictionary<string, object>();
            if (mapSeed is int seedValue)
                parameters["seed"] = seedValue;

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", parameters, deathPolicy);

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            var spawn = join.SpawnLocation();

            string? monsterId = null;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var spawnResult = await map.SpawnEntityAsync(new SpawnEntityRequest
                {
                    CreatureType = "monster",
                    X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z
                });
                if (spawnResult.Success) { monsterId = spawnResult.EntityId; break; }
            }
            Assert.That(monsterId, Is.Not.Null, "Expected at least one passable neighbour to place the monster.");

            return (map, player);
        }

        private static int HealthOf(WorldSnapshot snap, string entityId)
        {
            var placement = snap.Entities.FirstOrDefault(e => e.EntityId == entityId);
            Assert.That(placement, Is.Not.Null, $"Entity {entityId} missing from snapshot.");
            Assert.That(placement!.Properties.TryGetValue("HealthLevel", out var hp), Is.True, "HealthLevel not captured.");
            return int.Parse(hp!, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Ticks the map until the player's lethal hit resolves — either Health sticks at
        /// 0 (a down state, or permadeath, entered) or jumps back up (an instant respawn happened
        /// on the same tick that would otherwise have shown 0, so a plain "wait for &lt;= 0" check
        /// would spin forever under that model) — or gives up after a generous bound. AttackPower is
        /// 6 and players start at 100 HP, so this resolves within ~17 ticks; the bound is well above
        /// that to stay robust to future stat tuning.</summary>
        private async Task<WorldSnapshot> TickUntilPlayerDeathResolvesAsync(IGameMapGrain map, string player)
        {
            int? previousHealth = null;
            for (int i = 0; i < 40; i++)
            {
                await map.TickAsync(TimeSpan.FromSeconds(1));
                var snap = await map.GetWorldSnapshotAsync();
                var health = HealthOf(snap, player);
                if (health <= 0 || (previousHealth is not null && health > previousHealth))
                    return snap;
                previousHealth = health;
            }
            Assert.Fail("Player death never resolved within the tick bound.");
            throw new InvalidOperationException("unreachable");
        }

        /// <summary>Verifies "Player Death Outcomes" (instant respawn) in
        /// specs/death-respawn-policy/spec.md.</summary>
        [Test]
        public async Task InstantRespawn_NoDownState_NoPermadeath_PlayerRespawnsImmediately_AndCanActRightAway()
        {
            var policy = new DeathPolicy { Permadeath = false, DownStateEnabled = false, RespawnInvulnerabilityTicks = 0 };
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy);

            var snap = await TickUntilPlayerDeathResolvesAsync(map, player);

            // No down state: the same tick that brought HP to 0 already resolved the respawn.
            Assert.That(HealthOf(snap, player), Is.EqualTo(100), "Instant-respawn model must restore full health on the killing tick.");

            var move = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(move.Reason, Is.Not.EqualTo("You are downed and cannot act."), "A respawned player must be able to act immediately.");
        }

        /// <summary>Verifies "Player Death Outcomes" (down-then-respawn, the shipped Default) in
        /// specs/death-respawn-policy/spec.md.</summary>
        [Test]
        public async Task DownThenRespawn_Default_PlayerIsFrozenDuringTheDownWindow_ThenRespawns()
        {
            var policy = DeathPolicy.Default; // Permadeath=false, DownStateEnabled=true, ReviveWindowTicks=3
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy);

            var snap = await TickUntilPlayerDeathResolvesAsync(map, player);
            Assert.That(HealthOf(snap, player), Is.EqualTo(0), "Down state means the killing tick does not respawn yet.");

            var moveWhileDowned = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(moveWhileDowned.Success, Is.False);
            Assert.That(moveWhileDowned.Reason, Is.EqualTo("You are downed and cannot act."));

            // ReviveWindowTicks more ticks: the down countdown expires and (non-permadeath) respawns.
            for (int i = 0; i < policy.ReviveWindowTicks; i++)
                await map.TickAsync(TimeSpan.FromSeconds(1));

            var after = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(after, player), Is.EqualTo(100), "Down window elapsed: player must respawn at full health.");

            var moveAfterRespawn = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(moveAfterRespawn.Reason, Is.Not.EqualTo("You are downed and cannot act."));
        }

        /// <summary>Verifies "Player Death Outcomes" (instant permadeath) in
        /// specs/death-respawn-policy/spec.md.</summary>
        [Test]
        public async Task InstantPermadeath_NoDownState_Permadeath_PlayerNeverRespawns()
        {
            var policy = new DeathPolicy { Permadeath = true, DownStateEnabled = false };
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy);

            var snap = await TickUntilPlayerDeathResolvesAsync(map, player);
            Assert.That(HealthOf(snap, player), Is.EqualTo(0));

            var move = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(move.Success, Is.False);
            Assert.That(move.Reason, Is.EqualTo("You are downed and cannot act."));

            // Confirm it stays frozen — a permadeath player must never spontaneously respawn.
            for (int i = 0; i < 5; i++)
                await map.TickAsync(TimeSpan.FromSeconds(1));

            var stillFrozen = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(stillFrozen.Success, Is.False);
        }

        /// <summary>Verifies "Player Death Outcomes" (down-then-permadeath) in
        /// specs/death-respawn-policy/spec.md — distinguishes from the down-then-respawn model by
        /// confirming the player stays frozen (Corpse), not respawned, once the down window elapses.</summary>
        [Test]
        public async Task DownThenPermadeath_PlayerFrozenDuringDownWindow_ThenStaysFrozenAsCorpse()
        {
            var policy = new DeathPolicy { Permadeath = true, DownStateEnabled = true, ReviveWindowTicks = 2 };
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy);

            await TickUntilPlayerDeathResolvesAsync(map, player);

            for (int i = 0; i < policy.ReviveWindowTicks; i++)
                await map.TickAsync(TimeSpan.FromSeconds(1));

            var after = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(after, player), Is.EqualTo(0), "Down-then-permadeath must not restore health when the window expires.");

            var move = await map.MoveAsync(player, Aetherium.Model.RelativeDirection.Forward, 1);
            Assert.That(move.Success, Is.False, "A permadeath Corpse must still reject commands after the down window elapses.");
        }

        /// <summary>Verifies "Player Death Outcomes" (respawn invulnerability) in
        /// specs/death-respawn-policy/spec.md.</summary>
        [Test]
        public async Task RespawnInvulnerability_ProtectsAFreshRespawn_FromImmediateRedowning()
        {
            var policy = new DeathPolicy { Permadeath = false, DownStateEnabled = false, RespawnInvulnerabilityTicks = 3 };
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy);

            var snap = await TickUntilPlayerDeathResolvesAsync(map, player);
            Assert.That(HealthOf(snap, player), Is.EqualTo(100), "Instant respawn on the killing tick.");

            // The same monster is still adjacent (instant-respawn/DeathLocation-adjacent by
            // default). While invulnerable, further retaliation ticks must not reduce health.
            await map.TickAsync(TimeSpan.FromSeconds(1));
            var stillFull = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(stillFull, player), Is.EqualTo(100), "A freshly-respawned player must be untargetable during the invulnerability window.");
        }

        /// <summary>Verifies "Player Death Outcomes" (RespawnLocationPolicy.FixedCoordinates) in
        /// specs/death-respawn-policy/spec.md.</summary>
        [Test]
        public async Task RespawnLocation_FixedCoordinates_TeleportsToTheConfiguredLocation()
        {
            var policy = new DeathPolicy
            {
                Permadeath = false,
                DownStateEnabled = false,
                RespawnLocation = new RespawnLocationPolicy { Mode = RespawnLocationMode.FixedCoordinates, X = 2, Y = 2, Z = 0 },
            };
            // Pin the maze seed so (2,2,0) is reliably an open, unoccupied cell. Without a fixed
            // seed the maze was regenerated randomly each run, so (2,2,0) was sometimes a wall (or
            // occupied by a population monster); the respawn then fell back to a random WorldSpawn
            // cell that could be adjacent to another monster, which — with RespawnInvulnerabilityTicks
            // left at 0 — retaliated on the very same tick and knocked the fresh respawn below full
            // health. That made this test flaky (~1 in 3). Seeding the maze exercises the
            // FixedCoordinates teleport deterministically, which is what this test actually asserts.
            var (map, player) = await InitMapWithAdjacentMonsterAsync(policy, mapSeed: 47);

            var snap = await TickUntilPlayerDeathResolvesAsync(map, player);
            var placement = snap.Entities.First(e => e.EntityId == player);

            // The seeded maze guarantees the fixed cell is open, so the player must teleport exactly
            // onto the configured respawn coordinates.
            Assert.That((placement.X, placement.Y, placement.Z), Is.EqualTo((2, 2, 0)),
                "FixedCoordinates respawn must teleport the player onto the configured cell.");
        }
    }
}
