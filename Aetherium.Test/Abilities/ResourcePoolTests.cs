using NUnit.Framework;
using Aetherium.Server.Abilities;

namespace Aetherium.Test.Abilities
{
    /// <summary>Verifies "Resource Pools" (openspec/changes/add-abilities/specs/abilities/spec.md).</summary>
    [TestFixture]
    public class ResourcePoolTests
    {
        [Test]
        public void NormalPool_TrySpend_DrainsCurrent_WhenAffordable()
        {
            var mana = new ResourcePool("mana", max: 100, current: 50);

            Assert.That(mana.TrySpend(30), Is.True);
            Assert.That(mana.Current, Is.EqualTo(20));
        }

        [Test]
        public void NormalPool_TrySpend_Fails_WhenNotAffordable()
        {
            var mana = new ResourcePool("mana", max: 100, current: 10);

            Assert.That(mana.TrySpend(30), Is.False);
            Assert.That(mana.Current, Is.EqualTo(10), "A failed spend must not partially drain the pool.");
        }

        [Test]
        public void NormalPool_Regen_Continuous_FillsTowardMax()
        {
            var stamina = new ResourcePool("stamina", max: 100, regenPerTick: 10, current: 50,
                regenPolicy: ResourceRegenPolicy.Continuous);

            stamina.Regen(inCombat: true);

            Assert.That(stamina.Current, Is.EqualTo(60));
        }

        [Test]
        public void NormalPool_Regen_OutOfCombat_DoesNothingWhileInCombat()
        {
            var focus = new ResourcePool("focus", max: 100, regenPerTick: 10, current: 50,
                regenPolicy: ResourceRegenPolicy.OutOfCombat);

            focus.Regen(inCombat: true);
            Assert.That(focus.Current, Is.EqualTo(50), "OutOfCombat pools must not regen while in combat.");

            focus.Regen(inCombat: false);
            Assert.That(focus.Current, Is.EqualTo(60));
        }

        [Test]
        public void NormalPool_Regen_OnHit_DoesNotAutoRegen_UseGainOnHitInstead()
        {
            var pool = new ResourcePool("rage", max: 100, regenPerTick: 10, current: 0,
                regenPolicy: ResourceRegenPolicy.OnHit);

            pool.Regen(inCombat: true);
            Assert.That(pool.Current, Is.EqualTo(0), "OnHit pools do not passively regen.");

            pool.GainOnHit(15);
            Assert.That(pool.Current, Is.EqualTo(15));
        }

        [Test]
        public void InversePool_TrySpend_FillsTowardOverheat_InsteadOfDraining()
        {
            var heat = new ResourcePool("heat", max: 100, isInverse: true, overheatThreshold: 80, current: 0);

            Assert.That(heat.TrySpend(30), Is.True);
            Assert.That(heat.Current, Is.EqualTo(30), "Spending an inverse pool must raise Current, not lower it.");
        }

        [Test]
        public void InversePool_TrySpend_Fails_WhenWouldExceedOverheatThreshold()
        {
            var heat = new ResourcePool("heat", max: 100, isInverse: true, overheatThreshold: 80, current: 70);

            Assert.That(heat.TrySpend(20), Is.False, "Spending must be refused if it would push Current above the overheat threshold.");
            Assert.That(heat.Current, Is.EqualTo(70));
        }

        [Test]
        public void InversePool_Regen_Vents_TowardZero()
        {
            var heat = new ResourcePool("heat", max: 100, regenPerTick: 15, isInverse: true, current: 50);

            heat.Regen(inCombat: false);

            Assert.That(heat.Current, Is.EqualTo(35), "Regen on an inverse pool must drain (vent) toward zero, not fill toward Max.");
        }

        [Test]
        public void ResourcePools_TryGet_ReturnsAddedPool_ByTag()
        {
            var pools = new ResourcePools();
            pools.Add(new ResourcePool("mana", max: 50));

            Assert.That(pools.TryGet("mana", out var mana), Is.True);
            Assert.That(mana!.Max, Is.EqualTo(50));
            Assert.That(pools.TryGet("stamina", out _), Is.False);
        }
    }
}
