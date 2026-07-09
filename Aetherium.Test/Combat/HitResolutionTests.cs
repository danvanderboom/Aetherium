using System;
using NUnit.Framework;
using Aetherium;
using Aetherium.Server.Combat;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Pluggable Hit Resolution" (openspec/changes/deepen-combat-model/specs/combat/spec.md).</summary>
    [TestFixture]
    public class HitResolutionTests
    {
        [Test]
        public void AlwaysHitResolver_AlwaysHits_NeverCrits()
        {
            var resolver = new AlwaysHitResolver();
            var result = resolver.ResolveHit(new Character(), new Character());

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Critical, Is.False);
        }

        // A Random subclass that returns a fixed sequence, so hit/crit rolls are deterministic in tests.
        private sealed class ScriptedRandom : Random
        {
            private readonly double[] _values;
            private int _index;
            public ScriptedRandom(params double[] values) { _values = values; }
            public override double NextDouble() => _values[_index++];
        }

        [Test]
        public void RollHitResolver_RollBelowHitChance_Hits()
        {
            var attacker = new Character();
            attacker.Set(new Accuracy(0.9));
            var target = new Character();
            target.Set(new Evasion(0.1));
            // hitChance = 0.9 - 0.1 = 0.8; roll 0.5 < 0.8 => hit; crit roll 0.99 >= default 0.05 => no crit.
            var resolver = new RollHitResolver(new ScriptedRandom(0.5, 0.99));

            var result = resolver.ResolveHit(attacker, target);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Critical, Is.False);
        }

        [Test]
        public void RollHitResolver_RollAboveHitChance_Misses()
        {
            var attacker = new Character();
            attacker.Set(new Accuracy(0.5));
            var target = new Character();
            target.Set(new Evasion(0.4));
            // hitChance = 0.1 (clamped to 0.05 minimum floor is not hit since 0.1 > 0.05); roll 0.95 misses.
            var resolver = new RollHitResolver(new ScriptedRandom(0.95));

            var result = resolver.ResolveHit(attacker, target);

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Critical, Is.False);
        }

        [Test]
        public void RollHitResolver_HitChance_IsClampedToRange()
        {
            var attacker = new Character();
            attacker.Set(new Accuracy(2.0)); // absurd input
            var target = new Character();
            target.Set(new Evasion(-5.0));   // absurd input
            // Clamped hitChance = 0.99; roll 0.98 hits.
            var resolver = new RollHitResolver(new ScriptedRandom(0.98, 0.99));

            var result = resolver.ResolveHit(attacker, target);

            Assert.That(result.Hit, Is.True);
        }

        [Test]
        public void RollHitResolver_CritRollBelowCritChance_IsCritical()
        {
            var attacker = new Character();
            attacker.Set(new Accuracy(0.9));
            attacker.Set(new CritChance(0.5));
            var target = new Character();
            // hit roll 0.1 hits (chance 0.85); crit roll 0.2 < 0.5 => critical.
            var resolver = new RollHitResolver(new ScriptedRandom(0.1, 0.2));

            var result = resolver.ResolveHit(attacker, target);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Critical, Is.True);
        }

        [Test]
        public void RollHitResolver_MissingComponents_UseDefaults()
        {
            var attacker = new Character();
            var target = new Character();
            // Defaults: accuracy 0.9, evasion 0.05 => hitChance 0.85; roll 0.5 hits.
            var resolver = new RollHitResolver(new ScriptedRandom(0.5, 0.99));

            var result = resolver.ResolveHit(attacker, target);

            Assert.That(result.Hit, Is.True);
        }
    }
}
