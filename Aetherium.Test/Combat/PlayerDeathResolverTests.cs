using NUnit.Framework;
using Aetherium.Model.Combat;
using Aetherium.Server.Combat;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Unit coverage of the pure Permadeath×DownStateEnabled decision table (engine gap-analysis
    /// §4.11, Phase 2 — see openspec/changes/wire-death-respawn-live). Verifies "Player Death
    /// Outcomes" in specs/death-respawn-policy/spec.md.
    /// </summary>
    [TestFixture]
    public class PlayerDeathResolverTests
    {
        private static DeathPolicy Policy(bool permadeath, bool downStateEnabled) => new()
        {
            Permadeath = permadeath,
            DownStateEnabled = downStateEnabled,
        };

        [Test]
        public void InstantRespawn_NoPermadeath_NoDownState()
        {
            var outcome = PlayerDeathResolver.ResolveLethalHitOutcome(Policy(permadeath: false, downStateEnabled: false));
            Assert.That(outcome, Is.EqualTo(PlayerDeathOutcome.InstantRespawn));
        }

        [Test]
        public void InstantPermadeath_Permadeath_NoDownState()
        {
            var outcome = PlayerDeathResolver.ResolveLethalHitOutcome(Policy(permadeath: true, downStateEnabled: false));
            Assert.That(outcome, Is.EqualTo(PlayerDeathOutcome.InstantPermadeath));
        }

        [Test]
        public void EnterDowned_NoPermadeath_DownStateEnabled()
        {
            var outcome = PlayerDeathResolver.ResolveLethalHitOutcome(Policy(permadeath: false, downStateEnabled: true));
            Assert.That(outcome, Is.EqualTo(PlayerDeathOutcome.EnterDowned));
        }

        [Test]
        public void EnterDowned_Permadeath_DownStateEnabled()
        {
            var outcome = PlayerDeathResolver.ResolveLethalHitOutcome(Policy(permadeath: true, downStateEnabled: true));
            Assert.That(outcome, Is.EqualTo(PlayerDeathOutcome.EnterDowned));
        }

        [Test]
        public void DownedExpiry_RespawnsWhenNotPermadeath()
        {
            var outcome = PlayerDeathResolver.ResolveDownedOutcome(Policy(permadeath: false, downStateEnabled: true));
            Assert.That(outcome, Is.EqualTo(DownedExpiryOutcome.Respawn));
        }

        [Test]
        public void DownedExpiry_PermadeathsWhenPermadeath()
        {
            var outcome = PlayerDeathResolver.ResolveDownedOutcome(Policy(permadeath: true, downStateEnabled: true));
            Assert.That(outcome, Is.EqualTo(DownedExpiryOutcome.Permadeath));
        }
    }
}
