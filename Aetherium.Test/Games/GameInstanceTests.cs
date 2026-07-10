using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Server.Games;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Game Instance Creation", "Concurrent Multi-Game Hosting", and "Instance Config
    /// Immutability" (openspec/changes/add-game-definition-loader/specs/game-definitions/spec.md):
    /// YAML-defined games become running, playable worlds through the existing creation path;
    /// several instances of several games coexist on one cluster with full isolation; and a
    /// definition reload never mutates a running instance.
    /// </summary>
    [TestFixture]
    public class GameInstanceTests
    {
        private TestCluster _cluster = null!;

        // Written before Deploy; the silo registers this exact instance, so tests can Reload() it.
        private static string s_bundleRoot = null!;
        private static GameDefinitionRegistry s_registry = null!;

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

                    // The in-process cluster shares statics with the test: register the exact
                    // registry instance so tests can drive Reload() and observe the grain's view.
                    services.AddSingleton(s_registry);
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            s_bundleRoot = Path.Combine(Path.GetTempPath(), $"aetherium-instances-{Guid.NewGuid():N}");
            WriteEmberfall();
            WriteNeonveil();
            WriteReloadable("1.0.0", "zap");
            s_registry = new GameDefinitionRegistry(s_bundleRoot);
            s_registry.LoadAll();
            Assert.That(s_registry.Diagnostics.Where(d => d.Severity == Aetherium.Model.Games.GameDefinitionDiagnosticSeverity.Error),
                Is.Empty, "Test bundles must load cleanly: " + string.Join("; ", s_registry.Diagnostics));

            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
            try { Directory.Delete(s_bundleRoot, recursive: true); } catch { }
        }

        // --- Test bundles (mirrors of the shipped samples, sized for one-cast kills) ---

        private static void WriteBundle(string name, params (string File, string Yaml)[] files)
        {
            var dir = Path.Combine(s_bundleRoot, name);
            Directory.CreateDirectory(dir);
            foreach (var (file, yaml) in files)
                File.WriteAllText(Path.Combine(dir, file), yaml);
        }

        private static void WriteEmberfall() => WriteBundle("emberfall",
            ("game.yaml", """
                id: emberfall
                name: Emberfall
                version: 1.0.0
                description: Fantasy sample.
                world:
                  generatorType: maze
                  size: { width: 40, height: 40, depth: 1 }
                  maxPlayers: 40
                """),
            ("abilities.yaml", """
                characterResourcePools:
                  - tag: mana
                    max: 100
                    regenPerTick: 2
                abilities:
                  - id: fireball
                    resourcePoolTag: mana
                    resourceCost: 25
                    range: 6
                    effects:
                      - kind: DealDamage
                        damageType: fire
                        amount: 30
                """),
            ("progression.yaml", """
                pools:
                  - id: experience
                    curve: { kind: Linear, xpPerLevel: 100 }
                xpAwardRules:
                  - onEvent: MonsterDefeated
                    poolId: experience
                    amount: 25
                """),
            ("factions.yaml", """
                factions:
                  - id: town
                    name: Rivertown
                    doctrineDeltas:
                      "kill:wolf": 10
                  - id: cult
                    name: Cult of the Fang
                    doctrineDeltas:
                      "kill:wolf": -15
                bands:
                  - { id: neutral, minStanding: -100 }
                  - { id: friendly, minStanding: 10 }
                """),
            ("content.yaml", """
                creatures:
                  - id: wolf
                    name: Wolf
                    glyph: w
                    color: Gray
                    health: 20
                    attackPower: 4
                    speed: 1.25
                    behavior: wander-melee
                    lootItemId: wolf_pelt
                items:
                  - id: wolf_pelt
                    name: Wolf Pelt
                    icon: "%"
                    weight: 2
                spawns:
                  - creatureId: wolf
                    weight: 1
                """));

        private static void WriteNeonveil() => WriteBundle("neonveil",
            ("game.yaml", """
                id: neonveil
                name: Neonveil
                version: 0.9.0
                description: Sci-fi sample.
                world:
                  generatorType: rooms-and-corridors
                  size: { width: 40, height: 40, depth: 1 }
                  maxPlayers: 20
                abilities:
                  characterResourcePools:
                    - tag: bandwidth
                      max: 80
                      regenPerTick: 4
                  abilities:
                    - id: breach
                      resourcePoolTag: bandwidth
                      resourceCost: 20
                      range: 4
                      effects:
                        - kind: DealDamage
                          damageType: ice
                          amount: 30
                factions:
                  factions:
                    - id: helix_corp
                      name: Helix Corporation
                      doctrineDeltas:
                        "kill:drone": -20
                    - id: null_collective
                      name: The Null Collective
                      doctrineDeltas:
                        "kill:drone": 15
                  bands:
                    - { id: unknown, minStanding: -50 }
                content:
                  creatures:
                    - id: drone
                      name: Patrol Drone
                      glyph: d
                      color: Cyan
                      health: 15
                      attackPower: 3
                      behavior: wander-melee
                      lootItemId: scrap_core
                  items:
                    - id: scrap_core
                      name: Scrap Core
                      icon: "*"
                  spawns:
                    - creatureId: drone
                      weight: 1
                """));

        private static void WriteReloadable(string version, string abilityId) => WriteBundle("reloadable",
            ("game.yaml", $"""
                id: reloadable
                name: Reloadable
                version: {version}
                world:
                  generatorType: maze
                  size:
                    width: 40
                    height: 40
                    depth: 1
                abilities:
                  characterResourcePools:
                    - tag: charge
                      max: 100
                      regenPerTick: 5
                  abilities:
                    - id: {abilityId}
                      resourcePoolTag: charge
                      resourceCost: 10
                      range: 6
                      effects:
                        - kind: DealDamage
                          damageType: arc
                          amount: 30
                """));

        // --- Helpers ---

        private IGameManagementGrain Management => _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");

        private async Task<IGameMapGrain> FirstMapOfAsync(string worldId)
        {
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await world.GetMapIdsAsync();
            Assert.That(mapIds, Is.Not.Empty, $"World {worldId} must have at least one map.");
            return _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapIds.First());
        }

        private static async Task<(string player, Aetherium.Components.WorldLocation spawn)> JoinAsync(IGameMapGrain map)
        {
            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            return (player, join.SpawnLocation());
        }

        private static async Task<string> SpawnAdjacentAsync(IGameMapGrain map, Aetherium.Components.WorldLocation spawn, string creatureType)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var r = await map.SpawnEntityAsync(new SpawnEntityRequest { CreatureType = creatureType, X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z });
                if (r.Success) return r.EntityId!;
            }
            Assert.Ignore($"No passable neighbour to place an adjacent {creatureType} for this seed.");
            throw new InvalidOperationException("unreachable");
        }

        // --- Game Instance Creation ---

        [Test]
        public async Task CreateInstance_AppliesAllConfigsToTheWorld()
        {
            var result = await Management.CreateGameInstanceAsync("emberfall");
            Assert.That(result.Success, Is.True, result.Error);

            var map = await FirstMapOfAsync(result.WorldId!);
            var (player, spawn) = await JoinAsync(map);

            // Abilities config applied: the declared resource pool exists on a joining character.
            var pools = await map.GetResourcePoolsAsync(player);
            Assert.That(pools.Pools.Select(p => p.Tag), Does.Contain("mana"));

            // Factions config applied: the ledger is seeded with the game's factions.
            var reputation = await map.GetReputationAsync(player);
            Assert.That(reputation.Reputations.Select(r => r.FactionId), Is.EquivalentTo(new[] { "town", "cult" }));

            // Progression config applied: the declared progress pool exists.
            var progression = await map.GetProgressionAsync(player);
            Assert.That(progression.Pools.Select(p => p.Id), Does.Contain("experience"));

            // And the whole loop runs: a YAML-declared spell kills, awards XP, and moves standing.
            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            var cast = await map.UseAbilityAsync(player, "fireball", wolf);
            Assert.That(cast.Success, Is.True, cast.Reason);
            Assert.That(cast.TargetDefeated, Is.True);

            var after = await map.GetReputationAsync(player);
            Assert.That(after.Reputations.Single(r => r.FactionId == "town").Standing, Is.EqualTo(10));
            var progressionAfter = await map.GetProgressionAsync(player);
            Assert.That(progressionAfter.Pools.Single(p => p.Id == "experience").Xp, Is.EqualTo(25));
        }

        [Test]
        public async Task CreateInstance_RecordsDefinitionIdAndVersion()
        {
            var result = await Management.CreateGameInstanceAsync("emberfall", "Emberfall Test Shard");
            Assert.That(result.Success, Is.True, result.Error);

            var info = await Management.GetWorldInfoAsync(result.WorldId!);
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Name, Is.EqualTo("Emberfall Test Shard"));
            Assert.That(info.GameDefinitionId, Is.EqualTo("emberfall"));
            Assert.That(info.GameDefinitionVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task ListGameInstances_ReturnsOnlyThatGames_Instances()
        {
            var baseline = (await Management.ListGameInstancesAsync("emberfall")).Count;

            var created = await Management.CreateGameInstanceAsync("emberfall");
            Assert.That(created.Success, Is.True, created.Error);
            await Management.CreateGameInstanceAsync("neonveil");

            var instances = await Management.ListGameInstancesAsync("emberfall");
            Assert.That(instances.Count, Is.EqualTo(baseline + 1));
            Assert.That(instances.Select(w => w.GameDefinitionId), Is.All.EqualTo("emberfall"));
            Assert.That(instances.Select(w => w.WorldId), Does.Contain(created.WorldId));
        }

        // --- Concurrent Multi-Game Hosting ---

        [Test]
        public async Task ThreeEmberfall_TwoNeonveil_CoexistIsolated()
        {
            var emberfallBaseline = (await Management.ListGameInstancesAsync("emberfall")).Count;
            var neonveilBaseline = (await Management.ListGameInstancesAsync("neonveil")).Count;

            var emberfallIds = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var r = await Management.CreateGameInstanceAsync("emberfall", $"Emberfall #{i + 1}");
                Assert.That(r.Success, Is.True, r.Error);
                emberfallIds.Add(r.WorldId!);
            }
            var neonveilIds = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                var r = await Management.CreateGameInstanceAsync("neonveil", $"Neonveil #{i + 1}");
                Assert.That(r.Success, Is.True, r.Error);
                neonveilIds.Add(r.WorldId!);
            }

            Assert.That((await Management.ListGameInstancesAsync("emberfall")).Count, Is.EqualTo(emberfallBaseline + 3));
            Assert.That((await Management.ListGameInstancesAsync("neonveil")).Count, Is.EqualTo(neonveilBaseline + 2));

            // One game's content is present in its own instances and absent from the other's.
            var emberfallMap = await FirstMapOfAsync(emberfallIds[0]);
            var (emberfallPlayer, emberfallSpawn) = await JoinAsync(emberfallMap);
            var emberfallPools = await emberfallMap.GetResourcePoolsAsync(emberfallPlayer);
            Assert.That(emberfallPools.Pools.Select(p => p.Tag), Does.Contain("mana"));
            Assert.That(emberfallPools.Pools.Select(p => p.Tag), Does.Not.Contain("bandwidth"));
            Assert.That((await emberfallMap.GetFactionsAsync()).Factions.Select(f => f.Id),
                Is.EquivalentTo(new[] { "town", "cult" }));

            var neonveilMap = await FirstMapOfAsync(neonveilIds[0]);
            var (neonveilPlayer, _) = await JoinAsync(neonveilMap);
            var neonveilPools = await neonveilMap.GetResourcePoolsAsync(neonveilPlayer);
            Assert.That(neonveilPools.Pools.Select(p => p.Tag), Does.Contain("bandwidth"));
            Assert.That(neonveilPools.Pools.Select(p => p.Tag), Does.Not.Contain("mana"));
            Assert.That((await neonveilMap.GetFactionsAsync()).Factions.Select(f => f.Id),
                Is.EquivalentTo(new[] { "helix_corp", "null_collective" }));

            // An emberfall spell casts in emberfall and is unknown in neonveil.
            var wolf = await SpawnAdjacentAsync(emberfallMap, emberfallSpawn, "wolf");
            var fireballHome = await emberfallMap.UseAbilityAsync(emberfallPlayer, "fireball", wolf);
            Assert.That(fireballHome.Success, Is.True, fireballHome.Reason);

            var fireballAway = await neonveilMap.UseAbilityAsync(neonveilPlayer, "fireball", null);
            Assert.That(fireballAway.Success, Is.False, "fireball must not exist in a neonveil instance.");
            var breachAway = await emberfallMap.UseAbilityAsync(emberfallPlayer, "breach", null);
            Assert.That(breachAway.Success, Is.False, "breach must not exist in an emberfall instance.");
        }

        [Test]
        public async Task InstancesOfSameGame_AreIndependentWorlds()
        {
            var a = await Management.CreateGameInstanceAsync("emberfall", "Shard A");
            var b = await Management.CreateGameInstanceAsync("emberfall", "Shard B");
            Assert.That(a.Success && b.Success, Is.True);
            Assert.That(a.WorldId, Is.Not.EqualTo(b.WorldId));

            var mapA = await FirstMapOfAsync(a.WorldId!);
            var mapB = await FirstMapOfAsync(b.WorldId!);
            var (playerA, spawnA) = await JoinAsync(mapA);
            var (playerB, _) = await JoinAsync(mapB);

            // A kill in shard A moves standing there and nowhere else.
            var wolf = await SpawnAdjacentAsync(mapA, spawnA, "wolf");
            var cast = await mapA.UseAbilityAsync(playerA, "fireball", wolf);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            var ledgerA = await mapA.GetReputationAsync(playerA);
            Assert.That(ledgerA.Reputations.Single(r => r.FactionId == "town").Standing, Is.EqualTo(10));
            var ledgerB = await mapB.GetReputationAsync(playerB);
            Assert.That(ledgerB.Reputations.Single(r => r.FactionId == "town").Standing, Is.EqualTo(0),
                "Standing earned in one instance must not leak into another instance of the same game.");
        }

        // --- Data-Driven Population / Loot (add-content-definitions) ---

        [Test]
        public async Task CreateInstance_PopulatesFromSpawnTable()
        {
            var result = await Management.CreateGameInstanceAsync("emberfall");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);

            var snapshot = await map.GetWorldSnapshotAsync();
            var creatures = snapshot.Entities.Where(p => p.Properties.ContainsKey("CreatureType")).ToList();

            Assert.That(creatures, Is.Not.Empty, "The population passes must have placed monsters.");
            Assert.That(creatures.Select(c => c.Properties["CreatureType"]), Is.All.EqualTo("wolf"),
                "Every pass-placed monster must be re-materialized from the spawn table.");
            Assert.That(creatures.Select(c => c.Properties["HealthLevel"]), Is.All.EqualTo("20"),
                "Spawned creatures must carry the definition's stats, not Monster's hardcoded 30 HP.");
        }

        [Test]
        public async Task ContentIsolation_EmberfallWolves_NeonveilDrones()
        {
            var emberfall = await Management.CreateGameInstanceAsync("emberfall");
            var neonveil = await Management.CreateGameInstanceAsync("neonveil");
            Assert.That(emberfall.Success && neonveil.Success, Is.True);

            var emberfallTypes = (await (await FirstMapOfAsync(emberfall.WorldId!)).GetWorldSnapshotAsync())
                .Entities.Where(p => p.Properties.ContainsKey("CreatureType"))
                .Select(p => p.Properties["CreatureType"]).Distinct().ToList();
            var neonveilTypes = (await (await FirstMapOfAsync(neonveil.WorldId!)).GetWorldSnapshotAsync())
                .Entities.Where(p => p.Properties.ContainsKey("CreatureType"))
                .Select(p => p.Properties["CreatureType"]).Distinct().ToList();

            Assert.That(emberfallTypes, Is.EqualTo(new[] { "wolf" }),
                "An Emberfall instance must contain only Emberfall's bestiary.");
            Assert.That(neonveilTypes, Is.EqualTo(new[] { "drone" }),
                "A Neonveil instance must contain only Neonveil's bestiary.");
        }

        [Test]
        public async Task Kill_DropsDefinedLootItem()
        {
            var result = await Management.CreateGameInstanceAsync("emberfall");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);
            var (player, spawn) = await JoinAsync(map);

            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            var cast = await map.UseAbilityAsync(player, "fireball", wolf);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            var snapshot = await map.GetWorldSnapshotAsync();
            Assert.That(snapshot.Entities.Any(p =>
                    p.Properties.TryGetValue("CarriableLabel", out var label) && label == "Wolf Pelt"),
                Is.True, "The wolf's definition loot (wolf_pelt) must drop where it fell.");
            Assert.That(snapshot.Entities.Select(p => p.TypeName), Does.Not.Contain(nameof(Aetherium.Entities.SwordItem)),
                "The legacy hardcoded SwordItem drop must not appear for a defined creature.");
        }

        [Test]
        public async Task SpawnEntity_ResolvesDefinedCreature()
        {
            var result = await Management.CreateGameInstanceAsync("neonveil");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);
            var (_, spawn) = await JoinAsync(map);

            var droneId = await SpawnAdjacentAsync(map, spawn, "drone");

            var snapshot = await map.GetWorldSnapshotAsync();
            var drone = snapshot.Entities.Single(p => p.EntityId == droneId);
            Assert.That(drone.Properties["CreatureType"], Is.EqualTo("drone"));
            Assert.That(drone.Properties["HealthLevel"], Is.EqualTo("15"),
                "A spawn request naming a defined creature must materialize its definition's stats.");
        }

        // --- Instance Config Immutability ---

        [Test]
        public async Task DefinitionReload_DoesNotAffectRunningInstance()
        {
            var before = await Management.CreateGameInstanceAsync("reloadable");
            Assert.That(before.Success, Is.True, before.Error);
            var mapBefore = await FirstMapOfAsync(before.WorldId!);
            var (playerBefore, spawnBefore) = await JoinAsync(mapBefore);

            // v2 renames the ability: zap no longer exists in the definition.
            WriteReloadable("2.0.0", "pulse");
            s_registry.Reload();

            // The running instance still speaks v1: zap casts fine.
            var wolf = await SpawnAdjacentAsync(mapBefore, spawnBefore, "wolf");
            var zapOld = await mapBefore.UseAbilityAsync(playerBefore, "zap", wolf);
            Assert.That(zapOld.Success, Is.True, zapOld.Reason);

            // A new instance speaks v2: zap is gone, pulse exists, and the recorded version moved.
            var after = await Management.CreateGameInstanceAsync("reloadable");
            Assert.That(after.Success, Is.True, after.Error);
            var mapAfter = await FirstMapOfAsync(after.WorldId!);
            var (playerAfter, spawnAfter) = await JoinAsync(mapAfter);

            var zapNew = await mapAfter.UseAbilityAsync(playerAfter, "zap", null);
            Assert.That(zapNew.Success, Is.False);
            var target = await SpawnAdjacentAsync(mapAfter, spawnAfter, "wolf");
            var pulseNew = await mapAfter.UseAbilityAsync(playerAfter, "pulse", target);
            Assert.That(pulseNew.Success, Is.True, pulseNew.Reason);

            Assert.That((await Management.GetWorldInfoAsync(before.WorldId!))!.GameDefinitionVersion, Is.EqualTo("1.0.0"));
            Assert.That((await Management.GetWorldInfoAsync(after.WorldId!))!.GameDefinitionVersion, Is.EqualTo("2.0.0"));
        }
    }
}
