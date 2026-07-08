using NUnit.Framework;
using Aetherium.Server.Combat;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Death Policy Schema" and "Down-State Duration Resolution"
    /// (openspec/changes/add-death-respawn-policy/specs/death-respawn-policy/spec.md).</summary>
    [TestFixture]
    public class DeathPolicyTests
    {
        [Test]
        public void Default_ReproducesShippedBehavior()
        {
            var policy = DeathPolicy.Default;

            Assert.That(policy.Permadeath, Is.False);
            Assert.That(policy.CorpseRetentionTicks, Is.EqualTo(int.MaxValue), "Default must retain corpses forever, matching pre-policy behavior.");
            Assert.That(policy.DownStateEnabled, Is.True);
            Assert.That(policy.ReviveWindowTicks, Is.EqualTo(3), "Default must match DamagePipeline's pre-policy hardcoded dyingTicks of 3.");
        }

        [Test]
        public void ResolveDyingTicks_DownStateEnabled_ReturnsReviveWindow()
        {
            var policy = DeathPolicy.Default;
            policy.DownStateEnabled = true;
            policy.ReviveWindowTicks = 5;

            Assert.That(policy.ResolveDyingTicks(), Is.EqualTo(5));
        }

        [Test]
        public void ResolveDyingTicks_DownStateDisabled_ReturnsZero()
        {
            var policy = DeathPolicy.Default;
            policy.DownStateEnabled = false;
            policy.ReviveWindowTicks = 5;

            Assert.That(policy.ResolveDyingTicks(), Is.EqualTo(0), "No down state means an instant transition, regardless of ReviveWindowTicks.");
        }
    }
}
