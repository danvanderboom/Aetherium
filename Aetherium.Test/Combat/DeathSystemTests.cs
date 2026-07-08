using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Death State Transition" (openspec/changes/deepen-combat-model/specs/combat/spec.md).</summary>
    [TestFixture]
    public class DeathSystemTests
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
        public void Tick_DecrementsDyingCountdown_WithoutTransitioning()
        {
            var world = NewWorld();
            var entity = new Character();
            entity.Set(new WorldLocation(0, 0, 0));
            entity.Set(new Dying(2));
            world.AddEntity(entity);

            new DeathSystem().Tick(world);

            Assert.That(entity.Has<Dying>(), Is.True, "Entity must remain in the world, still Dying.");
            Assert.That(entity.Get<Dying>().TicksRemaining, Is.EqualTo(1));
            Assert.That(entity.Has<Corpse>(), Is.False);
        }

        [Test]
        public void Tick_CountdownReachesZero_TransitionsToCorpse()
        {
            var world = NewWorld();
            var entity = new Character();
            entity.Set(new WorldLocation(0, 0, 0));
            entity.Set(new Dying(1));
            world.AddEntity(entity);

            new DeathSystem().Tick(world);

            Assert.That(entity.Has<Dying>(), Is.False);
            Assert.That(entity.Has<Corpse>(), Is.True);
            Assert.That(world.Entities.ContainsKey(entity.EntityId), Is.True, "Corpse stays in the world, not deleted.");
        }

        [Test]
        public void Tick_EntityNotDying_IsIgnored()
        {
            var world = NewWorld();
            var entity = new Character();
            entity.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(entity);

            Assert.DoesNotThrow(() => new DeathSystem().Tick(world));
            Assert.That(entity.Has<Corpse>(), Is.False);
        }
    }
}
