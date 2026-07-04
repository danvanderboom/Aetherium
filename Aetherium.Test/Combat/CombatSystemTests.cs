using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Unit coverage of the pure combat resolution (P3-7). Combat was previously entirely inert
    /// (`if (false)` placeholder, no attack action anywhere).
    /// </summary>
    [TestFixture]
    public class CombatSystemTests
    {
        // A concrete no-frills entity (Entity is abstract) used to test attacking something with
        // no Health component.
        private sealed class Prop : Entity { }

        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character CharacterAt(World world, int x, int y, int z, int? hp = null)
        {
            var c = new Character();
            c.Set(new WorldLocation(x, y, z));
            if (hp.HasValue) c.Set(new Health(hp.Value, hp.Value));
            world.AddEntity(c);
            return c;
        }

        [Test]
        public void Attack_Adjacent_ReducesHealth_ButDoesNotDefeat()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var target = CharacterAt(world, 6, 5, 0, hp: 25);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.Damage, Is.EqualTo(CombatSystem.DefaultAttackDamage));
            Assert.That(result.RemainingHealth, Is.EqualTo(15));
            Assert.That(result.TargetDefeated, Is.False);
            Assert.That(world.Entities.ContainsKey(target.EntityId), Is.True, "Surviving target stays in the world.");
            Assert.That(target.Get<Health>().Level, Is.EqualTo(15));
        }

        [Test]
        public void Attack_Lethal_DefeatsAndRemovesTarget()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var target = CharacterAt(world, 6, 5, 0, hp: 10);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.RemainingHealth, Is.EqualTo(0));
            Assert.That(result.TargetDefeated, Is.True);
            Assert.That(world.Entities.ContainsKey(target.EntityId), Is.False, "Defeated target is removed.");
        }

        [Test]
        public void Attack_HealthBelowDamage_ClampsToZero_AndDefeats()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var target = CharacterAt(world, 6, 5, 0, hp: 5);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.RemainingHealth, Is.EqualTo(0), "Health floors at zero, not negative.");
            Assert.That(result.TargetDefeated, Is.True);
        }

        [Test]
        public void Attack_NotAdjacent_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var target = CharacterAt(world, 8, 5, 0, hp: 25);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.Success, Is.False);
            Assert.That(target.Get<Health>().Level, Is.EqualTo(25), "Out-of-reach target takes no damage.");
        }

        [Test]
        public void Attack_Self_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0, hp: 50);

            var result = new CombatSystem().TryAttack(world, attacker, attacker.EntityId);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void Attack_TargetWithoutHealth_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var rock = new Prop();
            rock.Set(new WorldLocation(6, 5, 0));
            world.AddEntity(rock);

            var result = new CombatSystem().TryAttack(world, attacker, rock.EntityId);

            Assert.That(result.Success, Is.False);
            Assert.That(world.Entities.ContainsKey(rock.EntityId), Is.True, "A target that can't be attacked is untouched.");
        }

        [Test]
        public void Attack_UnknownTarget_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);

            var result = new CombatSystem().TryAttack(world, attacker, "no-such-entity");

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void Character_SpawnsWithFullHealth()
        {
            // Combat depends on this: characters used to spawn Health(0,0), i.e. already dead.
            Assert.That(new Character().Get<Health>().Level, Is.EqualTo(100));
        }
    }
}
