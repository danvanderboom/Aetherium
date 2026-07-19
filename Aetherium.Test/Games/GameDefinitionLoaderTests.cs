using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Games;
using Aetherium.Server.Games;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Game Definition Bundle Loading"
    /// (openspec/changes/add-game-definition-loader/specs/game-definitions/spec.md): YAML bundles
    /// bind to the shipped config types, split section files are equivalent to inline sections,
    /// parsing is strict (typos are errors, not silent defaults), and one bad bundle never blocks
    /// the rest of a directory.
    /// </summary>
    [TestFixture]
    public class GameDefinitionLoaderTests
    {
        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"aetherium-gamedef-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private string WriteBundle(string bundleName, params (string FileName, string Yaml)[] files)
        {
            var dir = Path.Combine(_root, bundleName);
            Directory.CreateDirectory(dir);
            foreach (var (fileName, yaml) in files)
                File.WriteAllText(Path.Combine(dir, fileName), yaml);
            return dir;
        }

        private const string InlineSections = """
            death:
              permadeath: true
              dropOnDeath: All
              corpseRetentionTicks: 100
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
                      amount: 40
            progression:
              pools:
                - id: experience
                  curve: { kind: Linear, xpPerLevel: 100 }
              xpAwardRules:
                - onEvent: MonsterDefeated
                  poolId: experience
                  amount: 25
            factions:
              factions:
                - id: town
                  name: Rivertown
                  doctrineDeltas:
                    "kill:wolf": 10
              bands:
                - { id: neutral, minStanding: -100 }
            """;

        private const string Manifest = """
            id: testgame
            name: Test Game
            version: 1.0.0
            description: A test.
            tags: [sample]
            world:
              generatorType: maze
              size: { width: 40, height: 40, depth: 1 }
              maxPlayers: 10
            """;

        private static void AssertAllSectionsBound(GameDefinition definition)
        {
            Assert.That(definition.Id, Is.EqualTo("testgame"));
            Assert.That(definition.World.GeneratorType, Is.EqualTo("maze"));
            Assert.That(definition.World.Size!.Width, Is.EqualTo(40));

            Assert.That(definition.Death, Is.Not.Null);
            Assert.That(definition.Death!.Permadeath, Is.True);
            Assert.That(definition.Death.DropOnDeath, Is.EqualTo(Aetherium.Model.Combat.DropOnDeathPolicy.All));

            Assert.That(definition.Abilities, Is.Not.Null);
            var fireball = definition.Abilities!.Abilities.Single();
            Assert.That(fireball.Id, Is.EqualTo("fireball"));
            Assert.That(fireball.ResourceCost, Is.EqualTo(25));
            Assert.That(fireball.Effects.Single().Kind, Is.EqualTo(Aetherium.Model.Abilities.AbilityEffectKind.DealDamage));
            Assert.That(fireball.Effects.Single().Amount, Is.EqualTo(40));
            Assert.That(definition.Abilities.CharacterResourcePools.Single().Tag, Is.EqualTo("mana"));

            Assert.That(definition.Progression, Is.Not.Null);
            Assert.That(definition.Progression!.Pools.Single().Id, Is.EqualTo("experience"));
            Assert.That(definition.Progression.XpAwardRules.Single().Amount, Is.EqualTo(25));

            Assert.That(definition.Factions, Is.Not.Null);
            Assert.That(definition.Factions!.Factions.Single().DoctrineDeltas["kill:wolf"], Is.EqualTo(10));
            Assert.That(definition.Factions.Bands.Single().Id, Is.EqualTo("neutral"));
        }

        [Test]
        public void LoadBundle_SingleFile_BindsAllSections()
        {
            var dir = WriteBundle("single", ("game.yaml", Manifest + "\n" + InlineSections));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            AssertAllSectionsBound(result.Definition!);
        }

        // The economy recipe, inline under an `economy:` key (for game.yaml) and as a bare body (for a
        // sibling economy.yaml). Both must bind to GameDefinition.Economy — different loader code paths.
        private const string EconomyInline = """
            economy:
              goods:
                - { name: Spice, basePrice: 12.0, consumePerPop: 0.002 }
                - { name: Crystal, basePrice: 40.0, consumePerPop: 0.001 }
              coastalGood: Pearl
              coastalPerPop: 0.004
              production:
                - { biome: Desert, good: Spice, perPop: 0.02 }
                - { biome: Hills, good: Crystal, perPop: 0.01 }
            """;

        private const string EconomyBody = """
            goods:
              - { name: Spice, basePrice: 12.0, consumePerPop: 0.002 }
              - { name: Crystal, basePrice: 40.0, consumePerPop: 0.001 }
            coastalGood: Pearl
            coastalPerPop: 0.004
            production:
              - { biome: Desert, good: Spice, perPop: 0.02 }
              - { biome: Hills, good: Crystal, perPop: 0.01 }
            """;

        [Test]
        public void LoadBundle_EconomySection_BindsGoodsAndRecipe()
        {
            // Inline in game.yaml and as a sibling economy.yaml must both bind the recipe.
            foreach (var (label, files) in new[]
            {
                ("inline", new[] { ("game.yaml", Manifest + "\n" + EconomyInline) }),
                ("split",  new[] { ("game.yaml", Manifest), ("economy.yaml", EconomyBody) }),
            })
            {
                var dir = WriteBundle($"economy-{label}", files);
                var result = new GameDefinitionLoader().LoadBundle(dir);
                Assert.That(result.Success, Is.True, $"[{label}] " + string.Join("; ", result.Diagnostics));

                var eco = result.Definition!.Economy;
                Assert.That(eco, Is.Not.Null, $"[{label}] the economy section must bind");
                Assert.That(eco!.Goods.Select(g => g.Name), Is.EquivalentTo(new[] { "Spice", "Crystal" }), $"[{label}] goods");
                Assert.That(eco.Goods.Single(g => g.Name == "Crystal").BasePrice, Is.EqualTo(40.0).Within(1e-9), $"[{label}] price");
                Assert.That(eco.CoastalGood, Is.EqualTo("Pearl"), $"[{label}] coastal good");
                Assert.That(eco.Production.Single(p => p.Biome == "Desert").Good, Is.EqualTo("Spice"), $"[{label}] production");
            }
        }

        [Test]
        public void LoadBundle_SplitFiles_BindsAllSections()
        {
            // Same sections, but each in its conventional sibling file instead of inline.
            var sections = SplitTopLevelSections(InlineSections);
            var dir = WriteBundle("split",
                ("game.yaml", Manifest),
                ("death.yaml", sections["death"]),
                ("abilities.yaml", sections["abilities"]),
                ("progression.yaml", sections["progression"]),
                ("factions.yaml", sections["factions"]));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            AssertAllSectionsBound(result.Definition!);
        }

        [Test]
        public void LoadBundle_BindsPlayerAndCreatureVision()
        {
            // Per-character-type vision (directionality/FOV/range): the human player gets a
            // forward cone; a creature can carry its own. Proves the YAML `player.vision:` and
            // `creatures[].vision:` keys bind to the config model that the join path consumes.
            var dir = WriteBundle("vision", ("game.yaml", Manifest + """

                player:
                  vision:
                    directional: true
                    fieldOfView: 120
                content:
                  creatures:
                    - id: hunter
                      name: Hunter
                      health: 20
                      vision: { directional: true, fieldOfView: 70, range: 12 }
                    - id: blob
                      name: Blob
                      health: 10
                  spawns:
                    - { creatureId: hunter, weight: 1 }
                """));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            var def = result.Definition!;

            Assert.That(def.Player, Is.Not.Null);
            Assert.That(def.Player!.Vision, Is.Not.Null);
            Assert.That(def.Player.Vision!.Directional, Is.True);
            Assert.That(def.Player.Vision.FieldOfView, Is.EqualTo(120));

            var hunter = def.Content!.Creatures.Single(c => c.Id == "hunter");
            Assert.That(hunter.Vision, Is.Not.Null);
            Assert.That(hunter.Vision!.Directional, Is.True);
            Assert.That(hunter.Vision.FieldOfView, Is.EqualTo(70));
            Assert.That(hunter.Vision.Range, Is.EqualTo(12));

            // A creature with no vision block stays omnidirectional (null = legacy default).
            var blob = def.Content.Creatures.Single(c => c.Id == "blob");
            Assert.That(blob.Vision, Is.Null);
        }

        [Test]
        public void LoadBundle_DuplicateSection_IsRejected()
        {
            var sections = SplitTopLevelSections(InlineSections);
            var dir = WriteBundle("duplicate",
                ("game.yaml", Manifest + "\ndeath:\n  permadeath: true\n"),
                ("death.yaml", sections["death"]));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Section == "death" && d.Message.Contains("exactly one source")),
                Is.True, string.Join("; ", result.Diagnostics));
        }

        [Test]
        public void LoadBundle_MalformedYaml_ProducesDiagnosticAndSkips()
        {
            var dir = WriteBundle("malformed", ("game.yaml", "id: [unclosed\nname: {{{"));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Definition, Is.Null);
            Assert.That(result.Diagnostics, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(result.Diagnostics[0].Section, Is.EqualTo("manifest"));
        }

        [Test]
        public void LoadBundle_UnknownKey_IsRejected()
        {
            // "dammageType" is a typo of damageType: strict parsing must reject it,
            // never silently deserialize the effect with a default damage type.
            var dir = WriteBundle("typo", ("game.yaml", Manifest + """

                abilities:
                  abilities:
                    - id: zap
                      effects:
                        - kind: DealDamage
                          dammageType: fire
                          amount: 10
                """));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Message.Contains("dammageType")),
                Is.True, string.Join("; ", result.Diagnostics));
        }

        [Test]
        public void LoadBundle_GeneratorParameters_ScalarsAreTyped()
        {
            var dir = WriteBundle("params", ("game.yaml", """
                id: params
                name: Params
                version: 1.0.0
                world:
                  generatorType: maze
                  generatorParameters:
                    roomCount: 12
                    density: 0.5
                    debug: true
                    label: hello
                    quotedNumber: "42"
                """));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            var parameters = result.Definition!.World.GeneratorParameters;
            Assert.That(parameters["roomCount"], Is.EqualTo(12));
            Assert.That(parameters["density"], Is.EqualTo(0.5));
            Assert.That(parameters["debug"], Is.EqualTo(true));
            Assert.That(parameters["label"], Is.EqualTo("hello"));
            Assert.That(parameters["quotedNumber"], Is.EqualTo("42"), "A quoted scalar stays a string per YAML semantics.");
        }

        [Test]
        public void LoadBundle_ContentSection_BindsCreaturesItemsSpawns()
        {
            // Verifies "Content Config Data Model" (add-content-definitions): the content section
            // binds creatures/items/spawns, from a conventional sibling file like every section.
            var dir = WriteBundle("content",
                ("game.yaml", Manifest),
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
                      - id: salve
                        name: Healing Salve
                        icon: "+"
                        heal: { amount: 25, uses: 2 }
                      - id: blade
                        name: Blade
                        icon: "/"
                        weaponBonus: 7
                    spawns:
                      - creatureId: wolf
                        weight: 3
                    """));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            var content = result.Definition!.Content;
            Assert.That(content, Is.Not.Null);

            var wolf = content!.Creatures.Single();
            Assert.That(wolf.Id, Is.EqualTo("wolf"));
            Assert.That((wolf.Glyph, wolf.Color, wolf.Health, wolf.AttackPower, wolf.Speed),
                Is.EqualTo(("w", "Gray", 20, 4, 1.25)));
            Assert.That(wolf.Behavior, Is.EqualTo("wander-melee"));
            Assert.That(wolf.LootItemId, Is.EqualTo("wolf_pelt"));

            Assert.That(content.Items.Select(i => i.Id), Is.EqualTo(new[] { "wolf_pelt", "salve", "blade" }));
            var salve = content.Items.Single(i => i.Id == "salve");
            Assert.That((salve.Heal!.Amount, salve.Heal.Uses), Is.EqualTo((25, 2)));
            Assert.That(salve.WeaponBonus, Is.Null);
            Assert.That(content.Items.Single(i => i.Id == "blade").WeaponBonus, Is.EqualTo(7));

            var spawn = content.Spawns.Single();
            Assert.That((spawn.CreatureId, spawn.Weight), Is.EqualTo(("wolf", 3)));
        }

        [Test]
        public void LoadBundle_RulesSection_BindsTriggersConditionsActions()
        {
            // Verifies "ECA Rule Data Model" (add-eca-scripting): the rules section binds triggers,
            // conditions, and actions from a conventional sibling file like every section.
            var dir = WriteBundle("rules",
                ("game.yaml", Manifest),
                ("rules.yaml", """
                    rules:
                      - id: acolyte-summons-wolf
                        when: creature_died
                        if:
                          - kind: creature_type_is
                            creatureType: cult_acolyte
                          - kind: chance
                            probability: 0.5
                        do:
                          - kind: spawn_creature
                            creatureId: wolf
                          - kind: apply_status
                            target: Victim
                            statusId: slowed
                            durationTicks: 10
                            magnitude: 0.5
                    """));

            var result = new GameDefinitionLoader().LoadBundle(dir);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics));
            var rule = result.Definition!.Rules!.Rules.Single();
            Assert.That(rule.Id, Is.EqualTo("acolyte-summons-wolf"));
            Assert.That(rule.When, Is.EqualTo("creature_died"));
            Assert.That(rule.If.Select(c => c.Kind), Is.EqualTo(new[] { "creature_type_is", "chance" }));
            Assert.That(rule.If[0].CreatureType, Is.EqualTo("cult_acolyte"));
            Assert.That(rule.If[1].Probability, Is.EqualTo(0.5));

            var spawn = rule.Do[0];
            Assert.That((spawn.Kind, spawn.CreatureId), Is.EqualTo(("spawn_creature", "wolf")));
            var status = rule.Do[1];
            Assert.That(status.Kind, Is.EqualTo("apply_status"));
            Assert.That(status.Target, Is.EqualTo(Aetherium.Model.Eca.EcaActionTarget.Victim));
            Assert.That((status.StatusId, status.DurationTicks, status.Magnitude), Is.EqualTo(("slowed", 10, 0.5)));
        }

        [Test]
        public void LoadDirectory_BadBundle_DoesNotBlockOthers()
        {
            WriteBundle("bad", ("game.yaml", "id: {{{"));
            WriteBundle("good", ("game.yaml", Manifest));

            var registry = new GameDefinitionRegistry(_root);
            registry.LoadAll();

            Assert.That(registry.TryGet("testgame", out _), Is.True, "The valid bundle must load despite the broken sibling.");
            Assert.That(registry.Diagnostics.Any(d => d.BundlePath.Contains("bad")), Is.True);
        }

        /// <summary>Splits the inline-sections YAML into its top-level sections, dedenting each
        /// section's body so the same content can be written as standalone section files.</summary>
        private static System.Collections.Generic.Dictionary<string, string> SplitTopLevelSections(string yaml)
        {
            var sections = new System.Collections.Generic.Dictionary<string, string>();
            string? current = null;
            var body = new System.Text.StringBuilder();

            void Flush()
            {
                if (current != null)
                    sections[current] = body.ToString();
                body.Clear();
            }

            foreach (var line in yaml.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > 0 && line[0] != ' ' && line.TrimEnd().EndsWith(":"))
                {
                    Flush();
                    current = line.TrimEnd().TrimEnd(':');
                }
                else if (current != null)
                {
                    body.AppendLine(line.StartsWith("  ") ? line[2..] : line);
                }
            }
            Flush();
            return sections;
        }
    }
}
