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
using Aetherium.Model.Factions;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Factions
{
    /// <summary>
    /// Integration coverage of the live faction standing loop (engine gap-analysis §4.6, T0 of
    /// docs/factions-reputation.md — see openspec/changes/wire-factions-live): kill → kill:&lt;type&gt;
    /// tag → every faction's doctrine judges it independently → bands/ranks update, all from
    /// per-world <see cref="FactionConfig"/> data through the public grain API.
    /// </summary>
    [TestFixture]
    public class FactionStandingLiveTests
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

        // One damaging ability so the ability-kill path can be exercised.
        private static AbilityConfig TestAbilityConfig() => new()
        {
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "smite", Range = 1, Effects = { new() { Kind = AbilityEffectKind.DealDamage, DamageType = "physical", Amount = 30 } } },
            },
        };

        // Three factions judging the same world by different values:
        //  - town:       +10 for a wolf kill (rank at 10), starts at 0
        //  - beast_cult: -15 for a wolf kill (they revere wolves)
        //  - rangers:    +20 for a bandit kill only — no rule for wolves
        // Bands shared world-wide: hostile(-1000) / neutral(-100) / friendly(+10).
        private static FactionConfig TestFactionConfig() => new()
        {
            Factions = new List<FactionDefinition>
            {
                new()
                {
                    Id = "town", Name = "Rivertown",
                    DoctrineDeltas = new Dictionary<string, double> { ["kill:wolf"] = 10 },
                    RankRules = new List<RankRule> { new() { MinStanding = 10, RankId = "wolfsbane" } },
                },
                new()
                {
                    Id = "beast_cult", Name = "Cult of the Fang",
                    DoctrineDeltas = new Dictionary<string, double> { ["kill:wolf"] = -15 },
                },
                new()
                {
                    Id = "rangers", Name = "Free Rangers",
                    DoctrineDeltas = new Dictionary<string, double> { ["kill:bandit"] = 20 },
                },
                new()
                {
                    Id = "guild", Name = "Merchant Guild",
                    StartingStanding = 100,
                    RankRules = new List<RankRule> { new() { MinStanding = 50, RankId = "patron" } },
                },
            },
            Bands = new List<StandingBand>
            {
                new() { Id = "hostile", MinStanding = -1000 },
                new() { Id = "neutral", MinStanding = -100 },
                new() { Id = "friendly", MinStanding = 10 },
            },
        };

        private async Task<(IGameMapGrain map, string player, Aetherium.Components.WorldLocation spawn)> InitAndJoinAsync(bool withFactions = true)
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>(), null, TestAbilityConfig(), null,
                withFactions ? TestFactionConfig() : null);

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            return (map, player, join.SpawnLocation());
        }

        private async Task<string> SpawnAdjacentAsync(IGameMapGrain map, Aetherium.Components.WorldLocation spawn, string creatureType)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var r = await map.SpawnEntityAsync(new SpawnEntityRequest { CreatureType = creatureType, X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z });
                if (r.Success) return r.EntityId!;
            }
            Assert.Ignore($"No passable neighbour to place an adjacent {creatureType} for this seed.");
            throw new InvalidOperationException("unreachable");
        }

        /// <summary>Kills an adjacent Monster-class target (30 HP) with three 10-damage melee hits.</summary>
        private static async Task MeleeKillAsync(IGameMapGrain map, string player, string targetId)
        {
            for (int i = 0; i < 3; i++)
            {
                var result = await map.AttackAsync(player, targetId);
                Assert.That(result.Success, Is.True, result.Reason);
            }
        }

        private static ReputationDto RepOf(ReputationLedgerDto ledger, string factionId)
        {
            var rep = ledger.Reputations.FirstOrDefault(r => r.FactionId == factionId);
            Assert.That(rep, Is.Not.Null, $"Expected a reputation entry for {factionId}.");
            return rep!;
        }

        [Test]
        public async Task MonsterKill_Melee_MovesStanding_OppositelyForTwoFactions()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");

            await MeleeKillAsync(map, player, wolf);

            var ledger = await map.GetReputationAsync(player);
            Assert.That(RepOf(ledger, "town").Standing, Is.EqualTo(10), "The town honors a wolf kill.");
            Assert.That(RepOf(ledger, "beast_cult").Standing, Is.EqualTo(-15), "The cult condemns the same kill.");
            Assert.That(RepOf(ledger, "rangers").Standing, Is.EqualTo(0), "A faction with no rule for the tag is unaffected.");
        }

        [Test]
        public async Task MonsterKill_Ability_MovesStanding_SameAsMelee()
        {
            var (map, player, spawn) = await InitAndJoinAsync();
            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");

            var cast = await map.UseAbilityAsync(player, "smite", wolf);
            Assert.That(cast.Success, Is.True, cast.Reason);
            Assert.That(cast.TargetDefeated, Is.True);

            var ledger = await map.GetReputationAsync(player);
            Assert.That(RepOf(ledger, "town").Standing, Is.EqualTo(10), "An ability kill emits the same kill: tag as a melee kill.");
            Assert.That(RepOf(ledger, "beast_cult").Standing, Is.EqualTo(-15));
        }

        [Test]
        public async Task CreatureTypeTag_DistinguishesSpawnTypes_SharingAClass()
        {
            var (map, player, spawn) = await InitAndJoinAsync();

            // "wolf" and "bandit" both construct the Monster C# class; only the spawn-time
            // CreatureTypeTag can tell them apart at the kill site.
            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            await MeleeKillAsync(map, player, wolf);

            var afterWolf = await map.GetReputationAsync(player);
            Assert.That(RepOf(afterWolf, "rangers").Standing, Is.EqualTo(0), "kill:wolf must not trigger the rangers' kill:bandit rule.");

            var bandit = await SpawnAdjacentAsync(map, spawn, "bandit");
            await MeleeKillAsync(map, player, bandit);

            var afterBandit = await map.GetReputationAsync(player);
            Assert.That(RepOf(afterBandit, "rangers").Standing, Is.EqualTo(20), "kill:bandit must trigger it.");
        }

        [Test]
        public async Task KillCrossingThreshold_GrantsRank()
        {
            var (map, player, spawn) = await InitAndJoinAsync();

            var before = await map.GetReputationAsync(player);
            Assert.That(RepOf(before, "town").Ranks, Is.Empty, "No rank at join (standing 0 < threshold 10).");

            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            await MeleeKillAsync(map, player, wolf); // town 0 -> 10, crossing the wolfsbane threshold

            var after = await map.GetReputationAsync(player);
            Assert.That(RepOf(after, "town").Ranks, Does.Contain("wolfsbane"));
        }

        [Test]
        public async Task StandingBand_ReportedAndChanges_AsStandingMoves()
        {
            var (map, player, spawn) = await InitAndJoinAsync();

            var before = await map.GetReputationAsync(player);
            Assert.That(RepOf(before, "town").Band, Is.EqualTo("neutral"), "Standing 0 sits in the neutral band.");

            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            await MeleeKillAsync(map, player, wolf); // town 0 -> 10 crosses into friendly

            var after = await map.GetReputationAsync(player);
            Assert.That(RepOf(after, "town").Band, Is.EqualTo("friendly"), "A kill crossing a band boundary must be reflected in the reported band.");
        }

        [Test]
        public async Task StartingStandings_StampedAtJoin()
        {
            var (map, player, _) = await InitAndJoinAsync();

            var ledger = await map.GetReputationAsync(player);
            var guild = RepOf(ledger, "guild");
            Assert.That(guild.Standing, Is.EqualTo(100), "The configured starting standing is stamped at join.");
            Assert.That(guild.Ranks, Does.Contain("patron"), "A rank threshold already met at join is granted at join.");
            Assert.That(guild.Band, Is.EqualTo("friendly"));
        }

        [Test]
        public async Task NoFactionConfig_NoLedger_KillHookNoOps()
        {
            var (map, player, spawn) = await InitAndJoinAsync(withFactions: false);

            var ledger = await map.GetReputationAsync(player);
            Assert.That(ledger.Reputations, Is.Empty, "A world with no faction config stamps no ledger.");

            // A kill must not throw or invent standings.
            var monster = await SpawnAdjacentAsync(map, spawn, "wolf");
            await MeleeKillAsync(map, player, monster);

            var after = await map.GetReputationAsync(player);
            Assert.That(after.Reputations, Is.Empty);
        }
    }
}
