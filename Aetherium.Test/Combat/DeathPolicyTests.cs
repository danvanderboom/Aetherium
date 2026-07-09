using NUnit.Framework;
using Aetherium.Model.Combat;

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
            Assert.That(policy.RespawnLocation.Mode, Is.EqualTo(RespawnLocationMode.WorldSpawn));
            Assert.That(policy.RespawnInvulnerabilityTicks, Is.EqualTo(3));
            Assert.That(policy.PermadeathBehavior, Is.EqualTo(PermadeathSessionPolicy.Spectate));
        }

        [Test]
        public void RespawnLocationPolicy_WorldSpawnDefault_HasNoStrayCoordinates()
        {
            var location = RespawnLocationPolicy.WorldSpawnDefault;

            Assert.That(location.Mode, Is.EqualTo(RespawnLocationMode.WorldSpawn));
            Assert.That(location.LocationTag, Is.Null);
            Assert.That((location.X, location.Y, location.Z), Is.EqualTo((0, 0, 0)));
            Assert.That((location.OffsetX, location.OffsetY, location.OffsetZ), Is.EqualTo((0, 0, 0)));
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
