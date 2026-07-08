using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Corpse Expiry"
    /// (openspec/changes/add-death-respawn-policy/specs/death-respawn-policy/spec.md).</summary>
    [TestFixture]
    public class CorpseExpirySystemTests
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
        public void Tick_CorpseWithAge_BelowThreshold_AgesButIsNotRemoved()
        {
            var world = NewWorld();
            var corpse = new Character();
            corpse.Set(new WorldLocation(0, 0, 0));
            corpse.Set(new Corpse());
            corpse.Set(new CorpseAge(0));
            world.AddEntity(corpse);

            var policy = DeathPolicy.Default;
            policy.CorpseRetentionTicks = 3;

            new CorpseExpirySystem().Tick(world, policy);

            Assert.That(world.Entities.ContainsKey(corpse.EntityId), Is.True);
            Assert.That(corpse.Get<CorpseAge>().Ticks, Is.EqualTo(1));
        }

        [Test]
        public void Tick_CorpseWithAge_ReachesThreshold_IsRemoved()
        {
            var world = NewWorld();
            var corpse = new Character();
            corpse.Set(new WorldLocation(0, 0, 0));
            corpse.Set(new Corpse());
            corpse.Set(new CorpseAge(0));
            world.AddEntity(corpse);

            var policy = DeathPolicy.Default;
            policy.CorpseRetentionTicks = 2;

            new CorpseExpirySystem().Tick(world, policy);
            Assert.That(world.Entities.ContainsKey(corpse.EntityId), Is.True, "Not yet at threshold after 1 tick.");

            new CorpseExpirySystem().Tick(world, policy);
            Assert.That(world.Entities.ContainsKey(corpse.EntityId), Is.False, "Must be removed once age reaches CorpseRetentionTicks.");
        }

        [Test]
        public void Tick_CorpseWithoutAge_IsNeverRemoved_RegardlessOfPolicy()
        {
            var world = NewWorld();
            var corpse = new Character();
            corpse.Set(new WorldLocation(0, 0, 0));
            corpse.Set(new Corpse());
            // Deliberately no CorpseAge attached.
            world.AddEntity(corpse);

            var policy = DeathPolicy.Default;
            policy.CorpseRetentionTicks = 1; // Aggressive threshold — must still have no effect.

            for (int i = 0; i < 5; i++)
                new CorpseExpirySystem().Tick(world, policy);

            Assert.That(world.Entities.ContainsKey(corpse.EntityId), Is.True,
                "A Corpse with no CorpseAge must persist forever — this is the backward-compatible default, not a bug.");
        }

        [Test]
        public void Tick_EntityWithoutCorpse_IsIgnored()
        {
            var world = NewWorld();
            var livingEntity = new Character();
            livingEntity.Set(new WorldLocation(1, 1, 0));
            world.AddEntity(livingEntity);

            var policy = DeathPolicy.Default;
            policy.CorpseRetentionTicks = 1;

            Assert.DoesNotThrow(() => new CorpseExpirySystem().Tick(world, policy));
            Assert.That(world.Entities.ContainsKey(livingEntity.EntityId), Is.True);
        }
    }
}
