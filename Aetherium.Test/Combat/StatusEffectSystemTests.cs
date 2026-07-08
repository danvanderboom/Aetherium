using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Status Effects" (openspec/changes/deepen-combat-model/specs/combat/spec.md).</summary>
    [TestFixture]
    public class StatusEffectSystemTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test]
        public void Apply_SameId_RefreshesRatherThanStacks()
        {
            var effects = new StatusEffects();
            effects.Apply(new BurningEffect(durationTicks: 2, damagePerTick: 5));
            effects.Apply(new BurningEffect(durationTicks: 10, damagePerTick: 5));

            Assert.That(effects.Active.Count, Is.EqualTo(1));
            Assert.That(effects.Active[0].RemainingTicks, Is.EqualTo(10));
        }

        [Test]
        public void Tick_Burning_DealsDamage_AndDecrementsDuration()
        {
            var world = NewWorld();
            var target = new Character();
            target.Set(new WorldLocation(0, 0, 0));
            target.Set(new Health(20, 20));
            var effects = new StatusEffects();
            effects.Apply(new BurningEffect(durationTicks: 2, damagePerTick: 3));
            target.Set(effects);
            world.AddEntity(target);

            new StatusEffectSystem().Tick(world);

            Assert.That(target.Get<Health>().Level, Is.EqualTo(17));
            Assert.That(effects.Active[0].RemainingTicks, Is.EqualTo(1));
        }

        [Test]
        public void Tick_ExpiredEffect_IsRemoved()
        {
            var world = NewWorld();
            var target = new Character();
            target.Set(new WorldLocation(0, 0, 0));
            target.Set(new Health(20, 20));
            var effects = new StatusEffects();
            effects.Apply(new BurningEffect(durationTicks: 1, damagePerTick: 3));
            target.Set(effects);
            world.AddEntity(target);

            new StatusEffectSystem().Tick(world);

            Assert.That(effects.Active, Is.Empty, "Effect must be removed once RemainingTicks reaches 0.");
            Assert.That(target.Get<Health>().Level, Is.EqualTo(17), "Damage still applies on the tick it expires.");
        }

        [Test]
        public void Tick_EntityWithoutStatusEffects_IsIgnored()
        {
            var world = NewWorld();
            var bystander = new Character();
            bystander.Set(new WorldLocation(1, 1, 0));
            world.AddEntity(bystander);

            Assert.DoesNotThrow(() => new StatusEffectSystem().Tick(world));
        }

        [Test]
        public void SlowedAndProne_AreQueryableMarkers()
        {
            var effects = new StatusEffects();
            effects.Apply(new SlowedEffect(durationTicks: 3, speedMultiplier: 0.5));
            effects.Apply(new ProneEffect(durationTicks: 1));

            Assert.That(effects.Has("slowed"), Is.True);
            Assert.That(effects.Has("prone"), Is.True);
            Assert.That(effects.Has("burning"), Is.False);

            effects.TryGet("slowed", out var slowed);
            Assert.That(((SlowedEffect)slowed!).SpeedMultiplier, Is.EqualTo(0.5));
        }
    }
}
