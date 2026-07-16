using NUnit.Framework;
using Aetherium;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Model.Content;
using Aetherium.Server.Content;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Games
{
    /// <summary>
    /// Verifies "Content Survives Reactivation"
    /// (openspec/changes/add-content-definitions/specs/content-definitions/spec.md): the snapshot
    /// property round-trip carries creature identity and data-driven item components through
    /// <see cref="EntityFactory.ExtractProperties"/> → <c>Create</c>.
    /// </summary>
    [TestFixture]
    public class EntityFactorySnapshotTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.TileTypes["Monster"] = new TileType
            {
                Name = "Monster",
                Settings = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "MapCharacter", "M" },
                    { "BackgroundColor", System.ConsoleColor.Black.ToString() },
                    { "ForegroundColor", System.ConsoleColor.DarkRed.ToString() },
                },
            };
            return world;
        }

        [Test]
        public void CreatureTypeTag_RoundTrips()
        {
            var world = NewWorld();
            var wolf = new Monster(world);
            wolf.Set(new WorldLocation(4, 4, 0));
            wolf.Set(new CreatureTypeTag("wolf"));
            wolf.Set(new Health(5, 20)); // damaged — the level must survive too

            var placement = EntityPlacement.FromLocation(wolf.EntityId, nameof(Monster), wolf.Get<WorldLocation>());
            EntityFactory.ExtractProperties(wolf, placement);

            var rebuilt = new EntityFactory(world).Create(placement);

            Assert.That(rebuilt, Is.Not.Null);
            Assert.That(rebuilt!.Get<CreatureTypeTag>().Value, Is.EqualTo("wolf"));
            Assert.That(rebuilt.Get<Health>().Level, Is.EqualTo(5));
        }

        [Test]
        public void DataDrivenItem_ComponentsRoundTrip()
        {
            // A materialized definition item is a plain Item whose identity lives entirely in its
            // components — label, icon, weapon bonus, heal effect must all survive the snapshot.
            var world = NewWorld();
            var definition = new ItemDefinition
            {
                Id = "ember_blade",
                Name = "Ember Blade",
                Icon = "/",
                Weight = 3,
                WeaponBonus = 7,
                Heal = new HealEffectDefinition { Amount = 10, Uses = 2 },
            };
            var item = ContentCompiler.MaterializeItem(definition);
            item.Set(new WorldLocation(2, 3, 0));

            var placement = EntityPlacement.FromLocation(item.EntityId, nameof(Aetherium.Entities.Item), item.Get<WorldLocation>());
            EntityFactory.ExtractProperties(item, placement);

            var rebuilt = new EntityFactory(world).Create(placement);

            Assert.That(rebuilt, Is.Not.Null);
            var carriable = rebuilt!.Get<Carriable>();
            Assert.That((carriable.Label, carriable.Icon, carriable.Weight), Is.EqualTo(("Ember Blade", "/", 3)));
            Assert.That(rebuilt.Get<Weapon>().AttackBonus, Is.EqualTo(7));
            var consumable = rebuilt.Get<Consumable>();
            Assert.That((consumable.EffectValue, consumable.Uses), Is.EqualTo((10, 2)));
            Assert.That(consumable.EffectType, Is.EqualTo(ConsumableEffectType.HealthRestore));
        }
    }
}
