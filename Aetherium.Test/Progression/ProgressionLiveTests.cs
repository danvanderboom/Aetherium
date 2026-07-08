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
using Aetherium.Model.Progression;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Progression
{
    /// <summary>
    /// Integration coverage of the live progression loop (engine gap-analysis §4.4, Phase 2 — see
    /// openspec/changes/wire-progression-live): kill → XP → level → skill unlock → ability grant /
    /// attribute change / derived stat, all from per-world <see cref="ProgressionConfig"/> data.
    /// </summary>
    [TestFixture]
    public class ProgressionLiveTests
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

        // Abilities available on the test world (catalog): a one-shot "smite" and a weaker "fireball".
        private static AbilityConfig TestAbilityConfig() => new()
        {
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "smite", Range = 1, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 30 } } },
                new() { Id = "fireball", Range = 1, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "fire", Amount = 10 } } },
            },
        };

        // Progression: a combat pool (100 xp/level), a monster-kill rule awarding 100 combat XP,
        // vitality → max-health derivation, and three skills exercising the gates/effects.
        private static ProgressionConfig TestProgressionConfig(bool requireSkillToCast = false) => new()
        {
            Pools = new List<ProgressPoolDefinition>
            {
                new() { Id = "combat", Curve = new LevelCurveDefinition { Kind = LevelCurveKind.Linear, XpPerLevel = 100 }, StartingLevel = 1 },
            },
            StartingAttributes = new Dictionary<string, double> { ["vitality"] = 130 },
            AttributeDerivations = new List<AttributeDerivation>
            {
                new() { AttributeId = "vitality", DerivedStat = DerivedStat.HealthMax, PerPoint = 1, Base = 0 },
            },
            XpAwardRules = new List<XpAwardRule>
            {
                new() { OnEvent = XpAwardEvent.MonsterDefeated, PoolId = "combat", Amount = 100 },
            },
            Skills = new List<SkillDefinitionData>
            {
                new() { Id = "pyromancy", Description = "Learn fireball.", UnlocksAbilityId = "fireball", ModifiesAttributeId = "intellect", ModifierAmount = 5 },
                new() { Id = "combat_adept", Description = "Veteran training.", RequiredPoolId = "combat", RequiredLevel = 2, ModifiesAttributeId = "vitality", ModifierAmount = 20 },
                new() { Id = "power_strike", Description = "Advanced strike.", Prerequisites = new List<string> { "combat_adept" } },
            },
            RequireSkillToCastAbilities = requireSkillToCast,
        };

        private async Task<(IGameMapGrain map, string player, Aetherium.Components.WorldLocation spawn)> InitAndJoinAsync(bool requireSkillToCast = false)
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>(), null, TestAbilityConfig(), TestProgressionConfig(requireSkillToCast));

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            return (map, player, join.SpawnLocation());
        }

        private async Task<string> SpawnAdjacentMonsterAsync(IGameMapGrain map, Aetherium.Components.WorldLocation spawn)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var r = await map.SpawnEntityAsync(new SpawnEntityRequest { CreatureType = "monster", X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z });
                if (r.Success) return r.EntityId!;
            }
            Assert.Ignore("No passable neighbour to place an adjacent monster for this seed.");
            throw new InvalidOperationException("unreachable");
        }

        private static int PoolLevel(ProgressionStateDto dto, string poolId)
            => dto.Pools.First(p => p.Id == poolId).Level;

        private static int HealthOf(WorldSnapshot snap, string entityId)
        {
            var p = snap.Entities.First(e => e.EntityId == entityId);
            return int.Parse(p.Properties["HealthLevel"], System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---- XP Award On Kill ------------------------------------------------------------------

        [Test]
        public async Task MonsterKill_Melee_AwardsXp_AndLevelsPool()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            // Player base attack is 10; a monster has 30 HP → three melee hits kill it.
            for (int i = 0; i < 3; i++)
                await map.AttackAsync(player, monster);

            var prog = await map.GetProgressionAsync(player);
            Assert.That(prog.Pools.First(p => p.Id == "combat").Xp, Is.EqualTo(100), "A kill awards 100 combat XP per the rule.");
            Assert.That(PoolLevel(prog, "combat"), Is.EqualTo(2), "100 XP at 100/level crosses to level 2.");
        }

        [Test]
        public async Task MonsterKill_Ability_AwardsXp_AndLevelsPool()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            // "smite" one-shots a 30-HP monster — the same XP rule must fire as for a melee kill.
            var cast = await map.UseAbilityAsync(player, "smite", monster);
            Assert.That(cast.Success, Is.True, cast.Reason);
            Assert.That(cast.TargetDefeated, Is.True);

            var prog = await map.GetProgressionAsync(player);
            Assert.That(prog.Pools.First(p => p.Id == "combat").Xp, Is.EqualTo(100), "An ability kill awards the same XP as a melee kill.");
            Assert.That(PoolLevel(prog, "combat"), Is.EqualTo(2));
        }

        // ---- Skill Unlock & Ability Grant ------------------------------------------------------

        [Test]
        public async Task UnlockSkill_RespectsPrerequisitesAndPoolLevelGate()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            // combat_adept needs combat level 2; the player is level 1 → rejected.
            var tooEarly = await map.UnlockSkillAsync(player, "combat_adept");
            Assert.That(tooEarly.Success, Is.False);
            Assert.That(tooEarly.Result, Is.EqualTo("PoolLevelTooLow"));

            // power_strike needs combat_adept first → rejected.
            var missingPrereq = await map.UnlockSkillAsync(player, "power_strike");
            Assert.That(missingPrereq.Success, Is.False);
            Assert.That(missingPrereq.Result, Is.EqualTo("PrerequisitesNotMet"));

            // Reach combat level 2 by killing a monster, then both unlock.
            for (int i = 0; i < 3; i++)
                await map.AttackAsync(player, monster);

            var adept = await map.UnlockSkillAsync(player, "combat_adept");
            Assert.That(adept.Success, Is.True, adept.Reason);

            var powerStrike = await map.UnlockSkillAsync(player, "power_strike");
            Assert.That(powerStrike.Success, Is.True, powerStrike.Reason);
        }

        [Test]
        public async Task UnlockSkill_GrantsAbility_AndModifiesAttribute()
        {
            var (map, player, _) = await InitAndJoinAsync();

            var result = await map.UnlockSkillAsync(player, "pyromancy"); // grants fireball, +5 intellect
            Assert.That(result.Success, Is.True, result.Reason);

            var prog = await map.GetProgressionAsync(player);
            Assert.That(prog.GrantedAbilities, Does.Contain("fireball"), "Unlocking pyromancy grants its ability.");
            Assert.That(prog.Attributes.GetValueOrDefault("intellect"), Is.EqualTo(5), "Unlocking pyromancy modifies its attribute.");
            Assert.That(prog.UnlockedSkills, Does.Contain("pyromancy"));
        }

        // ---- Attribute-Derived Stats -----------------------------------------------------------

        [Test]
        public async Task AttributeDerivation_SetsMaxHealth_AtJoin()
        {
            var (map, player, _) = await InitAndJoinAsync();

            // vitality 130 → HealthMax 130; the join derivation also fills current health to the new max.
            var snap = await map.GetWorldSnapshotAsync();
            Assert.That(HealthOf(snap, player), Is.EqualTo(130), "Derived max health from vitality replaces the flat 100 default.");
        }

        [Test]
        public async Task AttributeDerivation_ReDerives_AfterSkillModifiesAttribute()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            for (int i = 0; i < 3; i++)   // reach combat level 2 to satisfy combat_adept's gate
                await map.AttackAsync(player, monster);

            var before = HealthOf(await map.GetWorldSnapshotAsync(), player);
            Assert.That(before, Is.EqualTo(130));

            await map.UnlockSkillAsync(player, "combat_adept"); // vitality +20 → 150

            var after = HealthOf(await map.GetWorldSnapshotAsync(), player);
            Assert.That(after, Is.EqualTo(150), "Raising vitality re-derives max health (and heals by the increase).");
        }

        // ---- Optional skill-gated casting ------------------------------------------------------

        [Test]
        public async Task RequireSkillToCastAbilities_True_GatesUngrantedCast_AllowsGranted()
        {
            var (map, player, spawn) = await InitAndJoinAsync(requireSkillToCast: true);
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            // fireball is in the catalog but not yet granted → rejected under the flag.
            var ungranted = await map.UseAbilityAsync(player, "fireball", monster);
            Assert.That(ungranted.Success, Is.False);
            Assert.That(ungranted.Reason, Is.EqualTo("Ability not yet learned"));

            // Unlock the granting skill, then the same cast succeeds.
            var unlock = await map.UnlockSkillAsync(player, "pyromancy");
            Assert.That(unlock.Success, Is.True, unlock.Reason);

            var granted = await map.UseAbilityAsync(player, "fireball", monster);
            Assert.That(granted.Success, Is.True, granted.Reason);
        }

        [Test]
        public async Task RequireSkillToCastAbilities_False_AnyCatalogAbilityCastable()
        {
            var (map, player, spawn) = await InitAndJoinAsync(requireSkillToCast: false);
            var monster = await SpawnAdjacentMonsterAsync(map, spawn);

            // Default flag: catalog membership is the only gate — no grant needed.
            var cast = await map.UseAbilityAsync(player, "fireball", monster);
            Assert.That(cast.Success, Is.True, cast.Reason);
        }
    }
}
