using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model.Abilities;
using Aetherium.Model.Content;
using Aetherium.Model.Eca;
using Aetherium.Model.Factions;
using Aetherium.Model.Games;
using Aetherium.Model.Progression;
using Aetherium.Server.Games;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Game Definition Validation"
    /// (openspec/changes/add-game-definition-loader/specs/game-definitions/spec.md): dangling
    /// cross-section references and duplicate ids are named errors with section context.
    /// </summary>
    [TestFixture]
    public class GameDefinitionValidatorTests
    {
        private static GameDefinition ValidDefinition() => new()
        {
            Id = "valid",
            Name = "Valid Game",
            Version = "1.0.0",
            World = new GameWorldDefinition { GeneratorType = "maze" },
            Abilities = new AbilityConfig
            {
                CharacterResourcePools = { new ResourcePoolDefinition { Tag = "mana", Max = 100 } },
                Abilities = { new AbilityDefinition { Id = "fireball", ResourcePoolTag = "mana", ResourceCost = 10 } },
            },
            Progression = new ProgressionConfig
            {
                Pools = { new ProgressPoolDefinition { Id = "experience" } },
                Skills =
                {
                    new SkillDefinitionData { Id = "pyromancy", UnlocksAbilityId = "fireball", RequiredPoolId = "experience", RequiredLevel = 2 },
                },
                XpAwardRules = { new XpAwardRule { OnEvent = XpAwardEvent.MonsterDefeated, PoolId = "experience", Amount = 10 } },
            },
            Factions = new FactionConfig
            {
                Factions =
                {
                    new FactionDefinition { Id = "town", Name = "Town" },
                    new FactionDefinition { Id = "cult", Name = "Cult" },
                },
                Relations = { new FactionRelationDefinition { FromFactionId = "town", ToFactionId = "cult", Disposition = FactionDispositionKind.War, Mutual = true } },
                Bands = { new StandingBand { Id = "neutral", MinStanding = -100 } },
            },
        };

        private static List<GameDefinitionDiagnostic> Validate(GameDefinition definition) =>
            new GameDefinitionValidator().Validate(definition, "test-bundle");

        [Test]
        public void ValidDefinition_ProducesNoDiagnostics()
        {
            Assert.That(Validate(ValidDefinition()), Is.Empty);
        }

        [Test]
        public void Topology_UnknownTiling_IsAnError()
        {
            var definition = ValidDefinition();
            definition.World.Topology = "dodecagon";

            var diagnostics = Validate(definition);
            Assert.That(diagnostics.Single().Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Error));
            Assert.That(diagnostics.Single().Message, Does.Contain("dodecagon"));
        }

        [Test]
        public void Topology_GeneratorWithoutSupport_IsAnError()
        {
            // rooms-and-corridors carves square rooms; it never declared hex support.
            var definition = ValidDefinition();
            definition.World.GeneratorType = "rooms-and-corridors";
            definition.World.Topology = "hex";

            var diagnostics = Validate(definition);
            Assert.That(diagnostics.Single().Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Error));
            Assert.That(diagnostics.Single().Message, Does.Contain("does not support topology 'hex'"));
        }

        [Test]
        public void Topology_HexCavesOnHex_IsValid()
        {
            var definition = ValidDefinition();
            definition.World.GeneratorType = "hex-caves";
            definition.World.Topology = "hex";

            Assert.That(Validate(definition), Is.Empty);
        }

        [Test]
        public void Topology_HexNativeGeneratorOnSquare_IsAnError()
        {
            // The reverse mismatch: hex-caves only makes sense on a hex lattice, so a
            // square (omitted-topology) world may not pick it.
            var definition = ValidDefinition();
            definition.World.GeneratorType = "hex-caves";
            definition.World.Topology = null;

            var diagnostics = Validate(definition);
            Assert.That(diagnostics.Single().Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Error));
            Assert.That(diagnostics.Single().Message, Does.Contain("does not support topology 'square'"));
        }

        [Test]
        public void Topology_UnknownGeneratorWithNonSquareTopology_IsAWarning()
        {
            var definition = ValidDefinition();
            definition.World.GeneratorType = "no-such-generator";
            definition.World.Topology = "tri";

            var diagnostics = Validate(definition);
            Assert.That(diagnostics.Single().Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Warning));
            Assert.That(diagnostics.Single().Message, Does.Contain("cannot verify"));
        }

        [Test]
        public void Skill_UnknownAbilityReference_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Progression!.Skills[0].UnlocksAbilityId = "icebolt";

            var diagnostics = Validate(definition);

            var finding = diagnostics.Single();
            Assert.That(finding.Section, Is.EqualTo("progression"));
            Assert.That(finding.Message, Does.Contain("pyromancy").And.Contain("icebolt"));
        }

        [Test]
        public void XpRule_UnknownPool_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Progression!.XpAwardRules[0].PoolId = "renown";

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Single().Message, Does.Contain("renown"));
            Assert.That(diagnostics.Single().Section, Is.EqualTo("progression"));
        }

        [Test]
        public void AbilityCost_UnknownResourcePool_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Abilities!.Abilities[0].ResourcePoolTag = "rage";

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Single().Message, Does.Contain("fireball").And.Contain("rage"));
            Assert.That(diagnostics.Single().Section, Is.EqualTo("abilities"));
        }

        [Test]
        public void Relation_UnknownFaction_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Factions!.Relations[0].ToFactionId = "empire";

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Single().Message, Does.Contain("empire"));
            Assert.That(diagnostics.Single().Section, Is.EqualTo("factions"));
        }

        [Test]
        public void DuplicateIds_WithinSection_AreErrors()
        {
            var definition = ValidDefinition();
            definition.Abilities!.Abilities.Add(new AbilityDefinition { Id = "fireball", ResourcePoolTag = "mana" });
            definition.Factions!.Factions.Add(new FactionDefinition { Id = "town", Name = "Other Town" });

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Any(d => d.Section == "abilities" && d.Message.Contains("Duplicate") && d.Message.Contains("fireball")), Is.True);
            Assert.That(diagnostics.Any(d => d.Section == "factions" && d.Message.Contains("Duplicate") && d.Message.Contains("town")), Is.True);
        }

        // --- Content (add-content-definitions) ---

        private static ContentConfig ValidContent() => new()
        {
            Creatures =
            {
                new CreatureDefinition { Id = "wolf", Name = "Wolf", Glyph = "w", Color = "Gray", Health = 20, LootItemId = "pelt" },
                new CreatureDefinition { Id = "acolyte", Name = "Acolyte", Glyph = "a", Color = "DarkMagenta", Health = 35 },
            },
            Items = { new ItemDefinition { Id = "pelt", Name = "Pelt", Icon = "%" } },
            Spawns = { new SpawnTableEntry { CreatureId = "wolf", Weight = 3 } },
        };

        [Test]
        public void Content_ValidSection_ProducesNoDiagnostics()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();

            Assert.That(Validate(definition), Is.Empty);
        }

        [Test]
        public void Content_DuplicateIds_AreErrors()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Content.Creatures.Add(new CreatureDefinition { Id = "wolf", Name = "Impostor Wolf" });
            definition.Content.Items.Add(new ItemDefinition { Id = "pelt", Name = "Impostor Pelt" });

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Any(d => d.Section == "content" && d.Message.Contains("Duplicate") && d.Message.Contains("wolf")), Is.True);
            Assert.That(diagnostics.Any(d => d.Section == "content" && d.Message.Contains("Duplicate") && d.Message.Contains("pelt")), Is.True);
        }

        [Test]
        public void Spawn_UnknownCreature_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Content.Spawns.Add(new SpawnTableEntry { CreatureId = "dragon", Weight = 1 });

            var diagnostics = Validate(definition);

            var finding = diagnostics.Single();
            Assert.That(finding.Section, Is.EqualTo("content"));
            Assert.That(finding.Message, Does.Contain("dragon"));
        }

        [Test]
        public void Loot_UnknownItem_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Content.Creatures[0].LootItemId = "golden_pelt";

            var diagnostics = Validate(definition);

            var finding = diagnostics.Single();
            Assert.That(finding.Section, Is.EqualTo("content"));
            Assert.That(finding.Message, Does.Contain("wolf").And.Contain("golden_pelt"));
        }

        [Test]
        public void Creature_UnknownBehaviorPreset_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Content.Creatures[0].Behavior = "ambush-pounce";

            var diagnostics = Validate(definition);

            var finding = diagnostics.Single();
            Assert.That(finding.Section, Is.EqualTo("content"));
            Assert.That(finding.Message, Does.Contain("ambush-pounce").And.Contain("wander-melee"));
        }

        [Test]
        public void Creature_UnparsableColor_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Content.Creatures[0].Color = "Crimson";

            var diagnostics = Validate(definition);

            Assert.That(diagnostics.Single().Message, Does.Contain("Crimson"));
        }

        [Test]
        public void Doctrine_KillTag_UnknownCreature_IsAWarning()
        {
            // The town judges kill:wolf (defined — silent) and kill:ghoul (undefined — a rule
            // that can never fire from this bestiary: warn, don't error). kill:player is exempt.
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Factions!.Factions[0].DoctrineDeltas["kill:wolf"] = 10;
            definition.Factions.Factions[0].DoctrineDeltas["kill:ghoul"] = -5;
            definition.Factions.Factions[0].DoctrineDeltas["kill:player"] = -100;

            var diagnostics = Validate(definition);

            var finding = diagnostics.Single();
            Assert.That(finding.Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Warning));
            Assert.That(finding.Message, Does.Contain("ghoul"));
        }

        // --- Rules (add-eca-scripting) ---

        private static EcaConfig RulesWith(params EcaRule[] rules) => new() { Rules = rules.ToList() };

        private static EcaRule SpawnWolfRule(string id = "r") => new()
        {
            Id = id,
            When = "creature_died",
            If = { new EcaConditionDescriptor { Kind = "creature_type_is", CreatureType = "wolf" } },
            Do = { new EcaActionDescriptor { Kind = "spawn_creature", CreatureId = "wolf" } },
        };

        [Test]
        public void Rules_ValidAgainstContent_ProduceNoDiagnostics()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();               // defines "wolf"
            definition.Rules = RulesWith(SpawnWolfRule());

            Assert.That(Validate(definition), Is.Empty);
        }

        [Test]
        public void Rule_DuplicateIds_AreErrors()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            definition.Rules = RulesWith(SpawnWolfRule("dup"), SpawnWolfRule("dup"));

            Assert.That(Validate(definition).Any(d => d.Section == "rules" && d.Message.Contains("Duplicate") && d.Message.Contains("dup")), Is.True);
        }

        [Test]
        public void Rule_UnknownTrigger_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            var rule = SpawnWolfRule();
            rule.When = "sunrise";
            definition.Rules = RulesWith(rule);

            Assert.That(Validate(definition).Any(d => d.Section == "rules" && d.Message.Contains("sunrise")), Is.True);
        }

        [Test]
        public void SpawnCreature_UnknownCreature_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();               // defines "wolf", not "dragon"
            var rule = SpawnWolfRule();
            rule.Do[0].CreatureId = "dragon";
            definition.Rules = RulesWith(rule);

            var finding = Validate(definition).Single(d => d.Section == "rules");
            Assert.That(finding.Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Error));
            Assert.That(finding.Message, Does.Contain("dragon"));
        }

        [Test]
        public void ApplyStatus_UnknownStatus_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            var rule = new EcaRule
            {
                Id = "curse",
                When = "creature_died",
                Do = { new EcaActionDescriptor { Kind = "apply_status", Target = EcaActionTarget.Killer, StatusId = "petrified" } },
            };
            definition.Rules = RulesWith(rule);

            var finding = Validate(definition).Single(d => d.Section == "rules");
            Assert.That(finding.Message, Does.Contain("petrified"));
        }

        [Test]
        public void Chance_OutOfRange_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            var rule = SpawnWolfRule();
            rule.If.Add(new EcaConditionDescriptor { Kind = "chance", Probability = 1.5 });
            definition.Rules = RulesWith(rule);

            Assert.That(Validate(definition).Any(d => d.Section == "rules" && d.Message.Contains("1.5")), Is.True);
        }

        [Test]
        public void DealDamage_NonPositiveAmount_IsAnError()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();
            var rule = new EcaRule
            {
                Id = "surge",
                When = "creature_died",
                Do = { new EcaActionDescriptor { Kind = "deal_damage", Target = EcaActionTarget.Killer, Amount = 0 } },
            };
            definition.Rules = RulesWith(rule);

            Assert.That(Validate(definition).Any(d => d.Section == "rules" && d.Message.Contains("deal_damage")), Is.True);
        }

        [Test]
        public void CreatureTypeIs_UnknownCreature_IsAWarning()
        {
            var definition = ValidDefinition();
            definition.Content = ValidContent();               // defines "wolf", not "ghoul"
            var rule = new EcaRule
            {
                Id = "r",
                When = "creature_died",
                If = { new EcaConditionDescriptor { Kind = "creature_type_is", CreatureType = "ghoul" } },
                Do = { new EcaActionDescriptor { Kind = "spawn_creature", CreatureId = "wolf" } },
            };
            definition.Rules = RulesWith(rule);

            var finding = Validate(definition).Single(d => d.Section == "rules");
            Assert.That(finding.Severity, Is.EqualTo(GameDefinitionDiagnosticSeverity.Warning));
            Assert.That(finding.Message, Does.Contain("ghoul"));
        }
    }
}
