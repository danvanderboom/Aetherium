using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Integration coverage of the deep combat pipeline (engine gap-analysis §4.2, Phase 1 — see
    /// openspec/changes/deepen-combat-model). Not yet wired into any live grain/command path;
    /// exercises hit resolution + mitigation + threat + death-state transition together.
    /// Contributes to "Per-Tag Damage Mitigation", "Pluggable Hit Resolution", "Death State
    /// Transition" (the Dying-entry scenario), and "Threat Ledger" in specs/combat/spec.md.
    /// </summary>
    [TestFixture]
    public class DamagePipelineTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character CharacterAt(World world, int hp)
        {
            var c = new Character();
            c.Set(new WorldLocation(0, 0, 0));
            c.Set(new Health(hp, hp));
            world.AddEntity(c);
            return c;
        }

        [Test]
        public void Resolve_Hit_AppliesMitigatedDamage_AndCreditsThreat()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var target = CharacterAt(world, 30);
            target.Set(new Resistances());
            target.Get<Resistances>().Set("slashing", new ResistanceEntry(flat: 2));

            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, target, packet, new AlwaysHitResolver());

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Damage, Is.EqualTo(8));
            Assert.That(target.Get<Health>().Level, Is.EqualTo(22));
            Assert.That(target.Get<ThreatTable>().ThreatByAttacker[attacker.EntityId], Is.EqualTo(8));
            Assert.That(target.Has<Dying>(), Is.False);
        }

        [Test]
        public void Resolve_Miss_DoesNotReduceHealth_OrCreditThreat()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var target = CharacterAt(world, 30);

            var resolver = new AlwaysMissResolver();
            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, target, packet, resolver);

            Assert.That(result.Hit, Is.False);
            Assert.That(target.Get<Health>().Level, Is.EqualTo(30));
            Assert.That(target.Has<ThreatTable>(), Is.False);
        }

        [Test]
        public void Resolve_Critical_AppliesMultiplier()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var target = CharacterAt(world, 30);

            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, target, packet, new AlwaysCritResolver());

            Assert.That(result.Critical, Is.True);
            Assert.That(result.Damage, Is.EqualTo(10 * DamagePipeline.CriticalMultiplier));
        }

        [Test]
        public void Resolve_LethalHit_EntersDyingState_DoesNotRemoveFromWorld()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var target = CharacterAt(world, 5);

            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, target, packet, new AlwaysHitResolver(), dyingTicks: 3);

            Assert.That(result.TargetEnteredDying, Is.True);
            Assert.That(target.Has<Dying>(), Is.True);
            Assert.That(target.Get<Dying>().TicksRemaining, Is.EqualTo(3));
            Assert.That(world.Entities.ContainsKey(target.EntityId), Is.True, "Lethally hit target stays in the world as Dying, not removed.");
        }

        [Test]
        public void Resolve_TargetAlreadyDying_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var target = CharacterAt(world, 30);
            target.Set(new Dying(2));

            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, target, packet, new AlwaysHitResolver());

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Reason, Does.Contain("already dying"));
        }

        [Test]
        public void Resolve_TargetWithoutHealth_Fails()
        {
            var world = NewWorld();
            var attacker = CharacterAt(world, 20);
            var prop = new Prop();
            prop.Set(new WorldLocation(1, 1, 0));
            world.AddEntity(prop);

            var packet = DamagePacket.Single("slashing", 10, attacker.EntityId);
            var result = new DamagePipeline().Resolve(attacker, prop, packet, new AlwaysHitResolver());

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Reason, Does.Contain("cannot be attacked"));
        }

        private sealed class Prop : Entity { }

        private sealed class AlwaysMissResolver : IHitResolver
        {
            public HitResult ResolveHit(Entity attacker, Entity target) => HitResult.Miss;
        }

        private sealed class AlwaysCritResolver : IHitResolver
        {
            public HitResult ResolveHit(Entity attacker, Entity target) => new(hit: true, critical: true);
        }
    }
}
