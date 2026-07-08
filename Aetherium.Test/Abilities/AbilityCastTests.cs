using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Abilities
{
    /// <summary>
    /// Integration coverage of the live ability cast path (engine gap-analysis §4.3, Phase 2 — see
    /// openspec/changes/wire-abilities-live). Drives casts through the public grain API on a live map
    /// whose abilities come entirely from a per-world <see cref="AbilityConfig"/> (the engine ships
    /// none), exercising damage, resource, cooldown, reach gating and per-tick upkeep.
    /// </summary>
    [TestFixture]
    public class AbilityCastTests
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

        /// <summary>The per-world ability content every test uses. The engine ships no abilities — these
        /// exist only here, proving abilities are campaign-supplied data.</summary>
        private static AbilityConfig TestConfig() => new()
        {
            CharacterResourcePools = new List<ResourcePoolDefinition>
            {
                new() { Tag = "energy", Max = 100, RegenPerTick = 5, RegenPolicy = ResourceRegenPolicyKind.Continuous, StartingValue = 40 },
            },
            Abilities = new List<AbilityDefinition>
            {
                // Free damaging abilities (no resource, no cooldown).
                new() { Id = "jab", Range = 1, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 10 } } },
                new() { Id = "smite", Range = 1, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 30 } } },
                // Self resource-modify (no target, no cost).
                new() { Id = "focus", Effects = { new() { Kind = AbilityEffectKind.ModifyResource, PoolTag = "energy", Delta = -15, ResourceTarget = AbilityEffectTarget.Caster } } },
                // Resource-gated: unaffordable variant and cooldown variant.
                new() { Id = "pricey", Range = 1, ResourcePoolTag = "energy", ResourceCost = 999, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 5 } } },
                new() { Id = "gated", Range = 1, ResourcePoolTag = "energy", ResourceCost = 30, Cooldown = 3, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 5 } } },
                // Cooldown-only (no resource cost), for cooldown-elapses coverage without resource interference.
                new() { Id = "ping", Cooldown = 3, Effects = { new() { Kind = AbilityEffectKind.ModifyResource, PoolTag = "energy", Delta = -1, ResourceTarget = AbilityEffectTarget.Caster } } },
            },
        };

        private async Task<(IGameMapGrain map, string player, Aetherium.Components.WorldLocation spawn)> InitMapAndJoinAsync(
            AbilityConfig config, DeathPolicy? deathPolicy = null)
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>(), deathPolicy, config);

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);

            return (map, player, join.SpawnLocation());
        }

        private async Task<string> SpawnAdjacentMonsterAsync(IGameMapGrain map, Aetherium.Components.WorldLocation spawn)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var r = await map.SpawnEntityAsync(new SpawnEntityRequest
                {
                    CreatureType = "monster", X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z
                });
                if (r.Success) return r.EntityId!;
            }
            Assert.Ignore("No passable neighbour to place an adjacent monster for this seed.");
            throw new InvalidOperationException("unreachable");
        }

        private async Task<(string monsterId, int distance)> SpawnMonsterAtDistanceAsync(
            IGameMapGrain map, Aetherium.Components.WorldLocation spawn, int minManhattan)
        {
            for (int radius = minManhattan; radius <= minManhattan + 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int dy = radius - Math.Abs(dx);
                    foreach (var sy in new[] { dy, -dy })
                    {
                        if (Math.Abs(dx) + Math.Abs(sy) < minManhattan) continue;
                        var r = await map.SpawnEntityAsync(new SpawnEntityRequest
                        {
                            CreatureType = "monster", X = spawn.X + dx, Y = spawn.Y + sy, Z = spawn.Z
                        });
                        if (r.Success) return (r.EntityId!, Math.Abs(dx) + Math.Abs(sy));
                    }
                }
            }
            Assert.Ignore("No passable far cell to place an out-of-reach monster for this seed.");
            throw new InvalidOperationException("unreachable");
        }

        private static int HealthOf(WorldSnapshot snap, string entityId)
        {
            var placement = snap.Entities.FirstOrDefault(e => e.EntityId == entityId);
            Assert.That(placement, Is.Not.Null, $"Entity {entityId} missing from snapshot.");
            Assert.That(placement!.Properties.TryGetValue("HealthLevel", out var hp), Is.True, "HealthLevel not captured.");
            return int.Parse(hp!, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double EnergyOf(ResourcePoolsDto dto)
        {
            var pool = dto.Pools.FirstOrDefault(p => p.Tag == "energy");
            Assert.That(pool, Is.Not.Null, "energy pool missing.");
            return pool!.Current;
        }

        // ---- Live Ability Cast Path ------------------------------------------------------------

        [Test]
        public async Task DamagingCast_ReducesTargetHealth_ThroughDamagePipeline()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            var result = await map.UseAbilityAsync(player, "jab", monster);
            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.DamageDealt, Is.EqualTo(10));

            var snap = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(snap, monster), Is.EqualTo(20), "A monster starts at 30 HP; a 10-damage cast leaves 20.");
        }

        [Test]
        public async Task DamagingCast_DefeatingMonster_EntersDying_AndDropsLoot_LikeMelee()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            var result = await map.UseAbilityAsync(player, "smite", monster); // 30 damage vs 30 HP
            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.TargetDefeated, Is.True);

            var stats = await map.GetCombatStatsAsync();
            Assert.That(stats.MonstersDefeated, Is.GreaterThanOrEqualTo(1));

            var snap = await map.GetWorldSnapshotAsync();
            // Like a melee kill: the monster persists as Dying (not removed) and loot is dropped.
            Assert.That(snap.Entities.Any(e => e.EntityId == monster), Is.True, "Defeated monster must persist as Dying, not be removed.");
            Assert.That(HealthOf(snap, monster), Is.EqualTo(0));
            Assert.That(snap.Entities.Any(e => e.TypeName == nameof(Aetherium.Entities.SwordItem)), Is.True, "An ability kill must drop loot like a melee kill.");
        }

        [Test]
        public async Task ResourceModifyCast_ChangesCastersOwnPool()
        {
            var (map, player, _) = await InitMapAndJoinAsync(TestConfig());

            Assert.That(EnergyOf(await map.GetResourcePoolsAsync(player)), Is.EqualTo(40), "Joining character starts with the world's configured energy pool.");

            var result = await map.UseAbilityAsync(player, "focus", null);
            Assert.That(result.Success, Is.True, result.Reason);

            Assert.That(EnergyOf(await map.GetResourcePoolsAsync(player)), Is.EqualTo(25), "focus drains 15 energy from the caster.");
        }

        [Test]
        public async Task Cast_UnknownAbility_IsRejected()
        {
            var (map, player, _) = await InitMapAndJoinAsync(TestConfig());

            var result = await map.UseAbilityAsync(player, "does-not-exist", null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("Unknown ability"));
        }

        [Test]
        public async Task NoAbilityConfig_EveryAbilityIsUnknown_AndNoPoolsStamped()
        {
            var (map, player, _) = await InitMapAndJoinAsync(config: null!); // no ability config at all

            var result = await map.UseAbilityAsync(player, "jab", null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("Unknown ability"), "The engine ships no abilities; a world with no config exposes none.");

            var pools = await map.GetResourcePoolsAsync(player);
            Assert.That(pools.Pools, Is.Empty, "A world declaring no pools stamps none onto its characters.");
        }

        // ---- Resource & Cooldown Gating --------------------------------------------------------

        [Test]
        public async Task Cast_OnCooldown_IsRejected()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            var first = await map.UseAbilityAsync(player, "gated", monster); // energy 40 >= 30, cooldown set to 3
            Assert.That(first.Success, Is.True, first.Reason);

            var second = await map.UseAbilityAsync(player, "gated", monster);
            Assert.That(second.Success, Is.False);
            Assert.That(second.Reason, Is.EqualTo("Ability is on cooldown"));
        }

        [Test]
        public async Task Cast_InsufficientResource_IsRejected_AndPoolUnchanged()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            var result = await map.UseAbilityAsync(player, "pricey", monster); // needs 999, has 40
            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("Insufficient resource"));

            Assert.That(EnergyOf(await map.GetResourcePoolsAsync(player)), Is.EqualTo(40), "A rejected cast must not spend any resource.");
            Assert.That(HealthOf(await map.GetWorldSnapshotAsync(), monster), Is.EqualTo(30), "A rejected cast must not damage the target.");
        }

        [Test]
        public async Task Cast_TargetOutOfReach_IsRejected()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var (monster, distance) = await SpawnMonsterAtDistanceAsync(map, spawn, minManhattan: 2);
            Assert.That(distance, Is.GreaterThan(1));

            var result = await map.UseAbilityAsync(player, "jab", monster); // jab range is 1
            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("Target is out of range"));
        }

        [Test]
        public async Task Cast_WhileDowned_IsRejected()
        {
            // Default death policy = down state on lethal hit. Drive the player to downed via monster
            // retaliation, then confirm a cast is rejected by the same actionable gate every command uses.
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig(), DeathPolicy.Default);
            await SpawnAdjacentMonsterAsync(map, spawn);

            bool downed = false;
            for (int i = 0; i < 40; i++)
            {
                await map.TickAsync(TimeSpan.FromSeconds(1));
                var snap = await map.GetWorldSnapshotAsync();
                if (HealthOf(snap, player) <= 0) { downed = true; break; }
            }
            Assert.That(downed, Is.True, "Expected the player to be downed within the tick bound.");

            var result = await map.UseAbilityAsync(player, "focus", null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("You are downed and cannot act."));
        }

        [Test]
        public async Task Cast_Success_PutsAbilityOnCooldown()
        {
            var (map, player, spawn) = await InitMapAndJoinAsync(TestConfig());
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            await map.UseAbilityAsync(player, "gated", monster);

            var cooldowns = await map.GetAbilityCooldownsAsync(player);
            Assert.That(cooldowns.ContainsKey("gated"), Is.True);
            Assert.That(cooldowns["gated"], Is.GreaterThan(0));
        }

        // ---- Tick Upkeep -----------------------------------------------------------------------

        [Test]
        public async Task Cooldown_TicksDown_OverTicks_ThenAbilityCastableAgain()
        {
            var (map, player, _) = await InitMapAndJoinAsync(TestConfig());

            var first = await map.UseAbilityAsync(player, "ping", null); // cooldown 3, no resource cost
            Assert.That(first.Success, Is.True, first.Reason);

            var blocked = await map.UseAbilityAsync(player, "ping", null);
            Assert.That(blocked.Success, Is.False, "Immediately recasting must be blocked by cooldown.");

            for (int i = 0; i < 3; i++)
                await map.TickAsync(TimeSpan.FromSeconds(1));

            var again = await map.UseAbilityAsync(player, "ping", null);
            Assert.That(again.Success, Is.True, "After the cooldown elapses, the ability must be castable again.");
        }

        [Test]
        public async Task ResourcePool_Regenerates_OverTicks()
        {
            var (map, player, _) = await InitMapAndJoinAsync(TestConfig());

            await map.UseAbilityAsync(player, "focus", null); // energy 40 -> 25
            var afterCast = EnergyOf(await map.GetResourcePoolsAsync(player));
            Assert.That(afterCast, Is.EqualTo(25));

            await map.TickAsync(TimeSpan.FromSeconds(1));
            await map.TickAsync(TimeSpan.FromSeconds(1));

            var afterTicks = EnergyOf(await map.GetResourcePoolsAsync(player));
            Assert.That(afterTicks, Is.GreaterThan(afterCast), "A Continuous-regen pool must recover over ticks.");
        }
    }
}
