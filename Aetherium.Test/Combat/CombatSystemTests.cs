using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
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

        // --- Slice 2: variable damage (AttackPower + Weapon) ---

        [Test]
        public void Attack_UsesAttackerAttackPower_InsteadOfDefault()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            attacker.Set(new AttackPower(25)); // overrides Character's default AttackPower(10)
            var target = CharacterAt(world, 6, 5, 0, hp: 100);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.Damage, Is.EqualTo(25));
            Assert.That(result.RemainingHealth, Is.EqualTo(75));
        }

        [Test]
        public void Attack_AddsBestWeaponBonus_FromInventory()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0); // AttackPower(10) from the Character ctor
            var inv = new Inventory();
            var sword = new SwordItem();                // Weapon("Sword", 5)
            inv.TryAdd(sword.EntityId, sword);
            attacker.Set(inv);
            var target = CharacterAt(world, 6, 5, 0, hp: 100);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId);

            Assert.That(result.Damage, Is.EqualTo(15), "10 base + 5 sword bonus.");
        }

        [Test]
        public void ComputeAttackDamage_PicksSingleBestWeapon_NoStacking()
        {
            var attacker = new Character(); // AttackPower(10)
            var inv = new Inventory();
            var dagger = new Item(); dagger.Set(new Weapon("Dagger", 3));
            var axe = new Item(); axe.Set(new Weapon("Axe", 7));
            inv.TryAdd(dagger.EntityId, dagger);
            inv.TryAdd(axe.EntityId, axe);
            attacker.Set(inv);

            Assert.That(CombatSystem.ComputeAttackDamage(attacker), Is.EqualTo(17),
                "10 base + best weapon (7), not 10 + 3 + 7.");
        }

        [Test]
        public void ComputeAttackDamage_NoAttackPowerComponent_UsesDefault()
        {
            var prop = new Prop(); // no AttackPower, no Inventory
            Assert.That(CombatSystem.ComputeAttackDamage(prop), Is.EqualTo(CombatSystem.DefaultAttackDamage));
        }

        [Test]
        public void Monster_HitsForLessThanBaseCharacter()
        {
            // Retaliation depth: a monster (AttackPower 6) chips rather than bursts.
            var world = NewWorld();
            var monster = new Monster(world);
            Assert.That(CombatSystem.ComputeAttackDamage(monster), Is.EqualTo(6));
            Assert.That(CombatSystem.ComputeAttackDamage(new Character()), Is.EqualTo(10));
        }

        [Test]
        public void Attack_RemoveOnDeathFalse_DefeatsButKeepsEntity()
        {
            // Monster retaliation "downs" a player: HP hits 0 but the entity survives so its
            // session and map bookkeeping stay consistent.
            var world = NewWorld();
            var attacker = CharacterAt(world, 5, 5, 0);
            var target = CharacterAt(world, 6, 5, 0, hp: 5);

            var result = new CombatSystem().TryAttack(world, attacker, target.EntityId, removeOnDeath: false);

            Assert.That(result.TargetDefeated, Is.True);
            Assert.That(result.RemainingHealth, Is.EqualTo(0));
            Assert.That(world.Entities.ContainsKey(target.EntityId), Is.True,
                "A downed target survives in the world when removeOnDeath is false.");
            Assert.That(target.Get<Health>().Level, Is.EqualTo(0));
        }
    }
}
