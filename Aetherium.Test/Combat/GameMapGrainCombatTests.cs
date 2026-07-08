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

        private static int HealthOf(WorldSnapshot snap, string entityId)
        {
            var placement = snap.Entities.FirstOrDefault(e => e.EntityId == entityId);
            Assert.That(placement, Is.Not.Null, $"Entity {entityId} missing from snapshot.");
            Assert.That(placement!.Properties.TryGetValue("HealthLevel", out var hp), Is.True, "HealthLevel not captured.");
            return int.Parse(hp!, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public async Task Tick_MonsterAdjacentToPlayer_Retaliates_DamagingButNotRemovingPlayer()
        {
            var (map, player, monsterId) = await InitMapWithAdjacentMonsterAsync();

            var before = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(before, player), Is.EqualTo(100), "Player starts at full health.");
            var monsterBefore = before.Entities.First(e => e.EntityId == monsterId);
            var (mx, my, mz) = (monsterBefore.X, monsterBefore.Y, monsterBefore.Z);

            // One tick: the adjacent monster attacks instead of wandering.
            await map.TickAsync(TimeSpan.FromSeconds(1));

            var after = await map.GetWorldSnapshotAsync();

            // Player survived the tick (downed players aren't removed) and took retaliation damage.
            // Damage is a positive multiple of the monster's AttackPower (6) — robust to any other
            // generated monster that also happened to be adjacent.
            var damageTaken = 100 - HealthOf(after, player);
            Assert.That(damageTaken, Is.GreaterThanOrEqualTo(6), "Adjacent monster should have hit the player.");
            Assert.That(damageTaken % 6, Is.EqualTo(0), "Each monster hit deals exactly 6 (AttackPower).");

            // The retaliating monster attacked rather than wandering, so it stayed put.
            var monsterAfter = after.Entities.First(e => e.EntityId == monsterId);
            Assert.That((monsterAfter.X, monsterAfter.Y, monsterAfter.Z), Is.EqualTo((mx, my, mz)),
                "A monster that attacks does not also move that tick.");
        }

        /// <summary>Verifies "Live NPC Tick Delegates to Behavior Tree" (target scoping) in
        /// specs/npc-behavior-trees/spec.md (openspec/changes/wire-npc-behavior-trees-live).</summary>
        [Test]
        public async Task Tick_TwoAdjacentMonsters_NoPlayerNearby_DoNotAttackEachOther()
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

            // Find two horizontally-adjacent passable cells and spawn a monster on each. With no
            // player joined on this map, the only possible attack target for either monster would
            // be the other monster — the behavior tree's target scoping (MonsterBehaviors.TargetsKey,
            // populated from GameMapGrain's joined-player list) must keep them from doing so.
            string? m1 = null, m2 = null;
            for (int x = 2; x <= 30 && m1 is null; x++)
            {
                for (int y = 2; y <= 30 && m1 is null; y++)
                {
                    var s1 = await map.SpawnEntityAsync(new SpawnEntityRequest { CreatureType = "monster", X = x, Y = y, Z = 0 });
                    if (!s1.Success) continue;
                    var s2 = await map.SpawnEntityAsync(new SpawnEntityRequest { CreatureType = "monster", X = x + 1, Y = y, Z = 0 });
                    if (s2.Success) { m1 = s1.EntityId; m2 = s2.EntityId; }
                }
            }
            Assert.That(m1, Is.Not.Null, "Expected two adjacent passable cells to place monsters on this map seed.");

            for (int i = 0; i < 3; i++)
                await map.TickAsync(TimeSpan.FromSeconds(1));

            var after = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(after, m1!), Is.EqualTo(30), "Monsters must never attack each other — only joined players are valid targets.");
            Assert.That(HealthOf(after, m2!), Is.EqualTo(30), "Monsters must never attack each other — only joined players are valid targets.");
        }

        [Test]
        public async Task Attack_KillingMonster_DropsLoot_AndRecordsStats()
        {
            var (map, player, monsterId) = await InitMapWithAdjacentMonsterAsync();

            await map.AttackAsync(player, monsterId);              // 30 → 20
            await map.AttackAsync(player, monsterId);              // 20 → 10
            var kill = await map.AttackAsync(player, monsterId);   // 10 → 0, defeated

            Assert.That(kill.TargetDefeated, Is.True);
            Assert.That(kill.DroppedLootEntityId, Is.Not.Null, "A slain monster drops loot.");
            Assert.That(kill.DroppedLootType, Is.EqualTo("SwordItem"));

            // Loot exists in canonical state, where the monster fell.
            var snap = await map.GetWorldSnapshotAsync();
            var loot = snap.Entities.FirstOrDefault(e => e.EntityId == kill.DroppedLootEntityId);
            Assert.That(loot, Is.Not.Null, "Dropped loot should be present in the world.");
            Assert.That(loot!.TypeName, Is.EqualTo("SwordItem"));

            // Analytics accrued: at least this kill + its 30 damage (other generated monsters, if
            // any were hit elsewhere, only add to these totals).
            var stats = await map.GetCombatStatsAsync();
            Assert.That(stats.MonstersDefeated, Is.GreaterThanOrEqualTo(1));
            Assert.That(stats.TotalDamageDealt, Is.GreaterThanOrEqualTo(30));
        }
    }
}
