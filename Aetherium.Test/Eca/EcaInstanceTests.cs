using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Server.Games;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Eca
{
    /// <summary>
    /// Verifies "Rules React to Creature Death" and "Rules Survive Reactivation"
    /// (openspec/changes/add-eca-scripting/specs/eca-scripting/spec.md): a YAML-defined rule fires
    /// live when a creature dies, spawning a content creature / dealing pipeline damage; a world with
    /// no rules behaves exactly as before; and rules recompile after a grain reactivation.
    /// </summary>
    [TestFixture]
    public class EcaInstanceTests
    {
        private TestCluster _cluster = null!;
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
                        // Keep NPCs from acting between our calls so the only damage a killer takes is
                        // the rule's — the death-surge assertion must not race monster retaliation.
                        opts.EnableNpcBehavior = false;
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
                    services.AddSingleton(s_registry);
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            s_bundleRoot = Path.Combine(Path.GetTempPath(), $"aetherium-eca-{Guid.NewGuid():N}");
            WriteReactiveBundle();
            WriteNoRulesBundle();
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

        // A one-cast-kill fantasy game: fireball (30) one-shots a 20-HP creature at range, so a kill
        // happens with no melee exchange. enemyCount 0 suppresses random population so the only
        // creatures are the ones a test spawns or a rule summons.
        private const string ContentAndAbilities = """
            world:
              generatorType: maze
              size: { width: 40, height: 40, depth: 1 }
              generatorParameters:
                enemyCount: 0
            abilities:
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
            content:
              creatures:
                - id: wolf
                  name: Wolf
                  glyph: w
                  color: Gray
                  health: 20
                - id: cult_acolyte
                  name: Cult Acolyte
                  glyph: a
                  color: DarkMagenta
                  health: 20
            """;

        private static void WriteBundle(string name, params (string File, string Yaml)[] files)
        {
            var dir = Path.Combine(s_bundleRoot, name);
            Directory.CreateDirectory(dir);
            foreach (var (file, yaml) in files)
                File.WriteAllText(Path.Combine(dir, file), yaml);
        }

        // Rules live in the conventional sibling file (root `rules:`), the same shape the shipped
        // emberfall bundle uses; content/abilities stay inline in the manifest.
        private static void WriteReactiveBundle() => WriteBundle("emberrules",
            ("game.yaml", $"""
                id: emberrules
                name: Emberrules
                version: 1.0.0
                {ContentAndAbilities}
                """),
            ("rules.yaml", """
                rules:
                  - id: acolyte-summons-wolf
                    when: creature_died
                    if:
                      - kind: creature_type_is
                        creatureType: cult_acolyte
                    do:
                      - kind: spawn_creature
                        creatureId: wolf
                  - id: wolf-death-surge
                    when: creature_died
                    if:
                      - kind: creature_type_is
                        creatureType: wolf
                    do:
                      - kind: deal_damage
                        target: Killer
                        amount: 15
                        damageType: physical
                """));

        private static void WriteNoRulesBundle() => WriteBundle("norules",
            ("game.yaml", $"""
                id: norules
                name: No Rules
                version: 1.0.0
                {ContentAndAbilities}
                """));

        // --- Helpers ---

        private IGameManagementGrain Management => _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");

        private async Task<IGameMapGrain> FirstMapOfAsync(string worldId)
        {
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapIds = await world.GetMapIdsAsync();
            Assert.That(mapIds, Is.Not.Empty);
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

        private static async Task<int> CountCreaturesAsync(IGameMapGrain map, string creatureType)
        {
            var snapshot = await map.GetWorldSnapshotAsync();
            return snapshot.Entities.Count(p =>
                p.Properties.TryGetValue("CreatureType", out var t) && t == creatureType);
        }

        private static async Task<int?> PlayerHealthAsync(IGameMapGrain map, string playerId)
        {
            var snapshot = await map.GetWorldSnapshotAsync();
            var placement = snapshot.Entities.FirstOrDefault(p => p.EntityId == playerId);
            if (placement != null && placement.Properties.TryGetValue("HealthLevel", out var hp)
                && int.TryParse(hp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return value;
            return null;
        }

        // --- Tests ---

        [Test]
        public async Task CreatureDeath_TriggersSpawnRule()
        {
            var result = await Management.CreateGameInstanceAsync("emberrules");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);
            var (player, spawn) = await JoinAsync(map);

            Assert.That(await CountCreaturesAsync(map, "wolf"), Is.EqualTo(0), "No wolves before the rule fires.");

            var acolyte = await SpawnAdjacentAsync(map, spawn, "cult_acolyte");
            var cast = await map.UseAbilityAsync(player, "fireball", acolyte);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            Assert.That(await CountCreaturesAsync(map, "wolf"), Is.EqualTo(1),
                "The acolyte's death must summon exactly one wolf via the spawn_creature rule.");
        }

        [Test]
        public async Task Rule_DealDamageToKiller_AppliesThroughPipeline()
        {
            var result = await Management.CreateGameInstanceAsync("emberrules");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);
            var (player, spawn) = await JoinAsync(map);

            var before = await PlayerHealthAsync(map, player);
            Assert.That(before, Is.Not.Null, "Player must have a captured health level.");

            var wolf = await SpawnAdjacentAsync(map, spawn, "wolf");
            var cast = await map.UseAbilityAsync(player, "fireball", wolf);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            var after = await PlayerHealthAsync(map, player);
            Assert.That(after, Is.EqualTo(before - 15),
                "The wolf's death-surge rule must deal 15 to the killer through the damage pipeline.");
        }

        [Test]
        public async Task NoEcaConfig_KillPathUnchanged()
        {
            var result = await Management.CreateGameInstanceAsync("norules");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);
            var (player, spawn) = await JoinAsync(map);

            var before = await PlayerHealthAsync(map, player);
            var acolyte = await SpawnAdjacentAsync(map, spawn, "cult_acolyte");
            var cast = await map.UseAbilityAsync(player, "fireball", acolyte);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            Assert.That(await CountCreaturesAsync(map, "wolf"), Is.EqualTo(0), "No rules → no summoned wolf.");
            Assert.That(await PlayerHealthAsync(map, player), Is.EqualTo(before), "No rules → the killer takes no death-surge.");
        }

        [Test]
        public async Task Rules_RecompileOnReactivation()
        {
            var result = await Management.CreateGameInstanceAsync("emberrules");
            Assert.That(result.Success, Is.True, result.Error);
            var map = await FirstMapOfAsync(result.WorldId!);

            // Force the map grain to deactivate; the next call reactivates it, re-running
            // OnActivateAsync which recompiles the rule runtime from persisted map state.
            await _cluster.Client.GetGrain<IManagementGrain>(0).ForceActivationCollection(TimeSpan.Zero);

            var (player, spawn) = await JoinAsync(map);
            var acolyte = await SpawnAdjacentAsync(map, spawn, "cult_acolyte");
            var cast = await map.UseAbilityAsync(player, "fireball", acolyte);
            Assert.That(cast.Success && cast.TargetDefeated, Is.True, cast.Reason);

            Assert.That(await CountCreaturesAsync(map, "wolf"), Is.EqualTo(1),
                "After reactivation the summon rule must still fire.");
        }
    }
}
