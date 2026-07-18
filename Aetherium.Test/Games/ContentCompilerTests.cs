using System;
using System.Linq;
using NUnit.Framework;
using Aetherium;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Model.Content;
using Aetherium.Model.ContentAtlas;
using Aetherium.Server.Content;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Data-Driven Population", "Data-Driven Loot", and "Per-World Entity-Kind Atlas"
    /// (openspec/changes/add-content-definitions/specs/content-definitions/spec.md) at the
    /// compiler tier: deterministic weighted draws, item materialization onto the existing
    /// component set, creature binding, and atlas registration.
    /// </summary>
    [TestFixture]
    public class ContentCompilerTests
    {
        private static ContentConfig SampleConfig() => new()
        {
            Creatures =
            {
                new CreatureDefinition { Id = "wolf", Name = "Wolf", Description = "A pack hunter.", Glyph = "w", Color = "Gray", Health = 20, AttackPower = 4, Speed = 1.25, LootItemId = "pelt" },
                new CreatureDefinition { Id = "acolyte", Name = "Acolyte", Glyph = "a", Color = "DarkMagenta", Health = 35, AttackPower = 7, Speed = 0.8 },
            },
            Items =
            {
                new ItemDefinition { Id = "pelt", Name = "Wolf Pelt", Icon = "%", Weight = 2 },
                new ItemDefinition { Id = "salve", Name = "Healing Salve", Icon = "+", Heal = new HealEffectDefinition { Amount = 25, Uses = 2 } },
                new ItemDefinition { Id = "blade", Name = "Ember Blade", Icon = "/", Weight = 3, WeaponBonus = 7 },
            },
            Spawns =
            {
                new SpawnTableEntry { CreatureId = "wolf", Weight = 3 },
                new SpawnTableEntry { CreatureId = "acolyte", Weight = 1 },
            },
        };

        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test]
        public void SpawnDraw_IsDeterministicPerSeed()
        {
            var catalog = ContentCompiler.Compile(SampleConfig());
            Assert.That(catalog.HasSpawnTable, Is.True);

            string[] Draw(int seed)
            {
                var rng = new Random(seed);
                return Enumerable.Range(0, 50).Select(_ => catalog.DrawSpawn(rng).Id).ToArray();
            }

            Assert.That(Draw(42), Is.EqualTo(Draw(42)), "Same seed must yield the same creature mix.");
            Assert.That(Draw(42).Distinct().Count(), Is.GreaterThan(1),
                "Both weighted rows must appear across 50 draws (weight 3:1).");
        }

        [Test]
        public void MaterializeItem_BindsCarriableConsumableWeapon()
        {
            var catalog = ContentCompiler.Compile(SampleConfig());

            var pelt = ContentCompiler.MaterializeItem(catalog.ItemsById["pelt"]);
            var carriable = pelt.Get<Carriable>();
            Assert.That((carriable.Label, carriable.Icon, carriable.Weight), Is.EqualTo(("Wolf Pelt", "%", 2)));
            Assert.That(pelt.Has<Consumable>(), Is.False);
            Assert.That(pelt.Has<Weapon>(), Is.False);

            var salve = ContentCompiler.MaterializeItem(catalog.ItemsById["salve"]);
            var consumable = salve.Get<Consumable>();
            Assert.That(consumable.EffectType, Is.EqualTo(ConsumableEffectType.HealthRestore));
            Assert.That((consumable.EffectValue, consumable.Uses), Is.EqualTo((25, 2)));

            var blade = ContentCompiler.MaterializeItem(catalog.ItemsById["blade"]);
            Assert.That(blade.Get<Weapon>().AttackBonus, Is.EqualTo(7));
            Assert.That(blade.Get<Weapon>().Name, Is.EqualTo("Ember Blade"));
        }

        [Test]
        public void ApplyCreature_BindsStatsGlyphAndIdentity()
        {
            var catalog = ContentCompiler.Compile(SampleConfig());
            var world = NewWorld();
            var monster = new Monster(world);

            ContentCompiler.ApplyCreature(monster, catalog.CreaturesById["wolf"], world);

            Assert.That((monster.Get<Health>().Level, monster.Get<Health>().MaxLevel), Is.EqualTo((20, 20)));
            Assert.That(monster.Get<AttackPower>().Amount, Is.EqualTo(4));
            Assert.That(monster.Get<CreatureTypeTag>().Value, Is.EqualTo("wolf"));
            var tile = monster.Get<Tile>().Type;
            Assert.That(tile.Settings["MapCharacter"], Is.EqualTo("w"));
            Assert.That(tile.Settings["ForegroundColor"], Is.EqualTo("Gray"));
            Assert.That(world.TileTypes.ContainsKey("Creature:wolf"), Is.True,
                "The per-creature tile type must be registered in the world.");
        }

        [Test]
        public void ApplyCreature_StampsPerTypeVision_OntoHeading()
        {
            // Each character type carries its own vision: a directional creature gets a forward
            // cone on its HasHeading (which the agent-perception path reads to filter sight);
            // a creature with no vision block stays omnidirectional.
            var world = NewWorld();

            var seer = new Monster(world);
            ContentCompiler.ApplyCreature(seer, new Aetherium.Model.Content.CreatureDefinition
            {
                Id = "seer", Name = "Seer", Health = 10,
                Vision = new Aetherium.Model.Content.VisionConfig { Directional = true, FieldOfView = 70, Range = 12 },
            }, world);
            var seerHeading = seer.Get<HasHeading>();
            Assert.That(seerHeading.IsDirectional, Is.True);
            Assert.That(seerHeading.FieldOfViewDegrees, Is.EqualTo(70));
            Assert.That(seerHeading.ViewRange, Is.EqualTo(12));

            var blind = new Monster(world);
            ContentCompiler.ApplyCreature(blind, new Aetherium.Model.Content.CreatureDefinition
            {
                Id = "blob", Name = "Blob", Health = 10,
            }, world);
            Assert.That(blind.Get<HasHeading>().IsDirectional, Is.False,
                "a creature with no vision block stays omnidirectional");
        }

        [Test]
        public void ApplyCreature_PreserveHealthLevel_KeepsCurrentDamage()
        {
            // The snapshot re-hydration path (spec: "Content Survives Reactivation"): a wolf at
            // 5 HP must come back a wolf at 5 HP, not fully healed.
            var catalog = ContentCompiler.Compile(SampleConfig());
            var world = NewWorld();
            var monster = new Monster(world);
            monster.Set(new Health(5, 30));

            ContentCompiler.ApplyCreature(monster, catalog.CreaturesById["wolf"], world, preserveHealthLevel: true);

            Assert.That((monster.Get<Health>().Level, monster.Get<Health>().MaxLevel), Is.EqualTo((5, 20)));
        }

        [Test]
        public void Atlas_RegistersDefinedEntityKinds()
        {
            var catalog = ContentCompiler.Compile(SampleConfig());

            Assert.That(catalog.Atlas.Contains(ContentAtlasCategory.EntityKind, "wolf"), Is.True);
            Assert.That(catalog.Atlas.Contains(ContentAtlasCategory.EntityKind, "acolyte"), Is.True);
            Assert.That(catalog.Atlas.Contains(ContentAtlasCategory.EntityKind, "pelt"), Is.True);
            Assert.That(catalog.Atlas.EntityKindTags["wolf"].Description, Is.EqualTo("A pack hunter."));
            Assert.That(catalog.Atlas.Contains(ContentAtlasCategory.EntityKind, "monster"), Is.True,
                "Engine default entity kinds must remain alongside the defined ones.");
        }
    }
}
