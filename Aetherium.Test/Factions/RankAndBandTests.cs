using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.Server.Factions;
using Aetherium.Model.Factions;

namespace Aetherium.Test.Factions
{
    /// <summary>
    /// Verifies "Declarative Rank Grants" and "Standing Bands"
    /// (openspec/changes/wire-factions-live/specs/factions/spec.md) at the mechanism level:
    /// monotonic threshold-based rank grants and highest-threshold-at-or-below band resolution.
    /// </summary>
    [TestFixture]
    public class RankAndBandTests
    {
        private static readonly List<RankRule> Rules = new()
        {
            new RankRule { MinStanding = 100, RankId = "friend" },
            new RankRule { MinStanding = 500, RankId = "champion" },
        };

        [Test]
        public void RankEvaluator_BelowThreshold_NoGrant()
        {
            var reputation = new Reputation("town", standing: 99);
            RankEvaluator.Apply(reputation, Rules);
            Assert.That(reputation.Ranks, Is.Empty);
        }

        [Test]
        public void RankEvaluator_AtOrAboveThreshold_GrantsOnce()
        {
            var reputation = new Reputation("town", standing: 100);

            RankEvaluator.Apply(reputation, Rules);
            RankEvaluator.Apply(reputation, Rules); // re-evaluation must not duplicate

            Assert.That(reputation.Ranks, Is.EqualTo(new[] { "friend" }));
        }

        [Test]
        public void RankEvaluator_MultipleThresholds_AllMetGranted()
        {
            var reputation = new Reputation("town", standing: 600);
            RankEvaluator.Apply(reputation, Rules);
            Assert.That(reputation.Ranks, Is.EquivalentTo(new[] { "friend", "champion" }));
        }

        [Test]
        public void RankEvaluator_DoesNotRevoke_WhenStandingFalls()
        {
            // Earn the rank at high standing, then drive standing down through the public
            // doctrine path and re-evaluate: the rank must survive (monotonic grants).
            var ledger = new ReputationLedger();
            ledger.Add(new Reputation("town", standing: 150));
            RankEvaluator.Apply(ledger.ByFaction["town"], Rules);
            Assert.That(ledger.ByFaction["town"].Ranks, Does.Contain("friend"));

            var doctrine = new FactionDoctrine();
            doctrine.SetDelta("atrocity", -400);
            var town = new Faction("town", "Rivertown", doctrine);
            ledger.ApplyAction(town, "atrocity"); // 150 -> -250

            RankEvaluator.Apply(ledger.ByFaction["town"], Rules);
            Assert.That(ledger.ByFaction["town"].Standing, Is.EqualTo(-250));
            Assert.That(ledger.ByFaction["town"].Ranks, Does.Contain("friend"),
                "A rank, once granted, is kept even when standing later falls below its threshold.");
        }

        private static readonly List<StandingBand> Bands = new()
        {
            new StandingBand { Id = "hostile", MinStanding = -1000 },
            new StandingBand { Id = "neutral", MinStanding = -100 },
            new StandingBand { Id = "friendly", MinStanding = 200 },
        };

        [Test]
        public void BandResolver_ResolvesHighestBandAtOrBelowStanding()
        {
            Assert.That(BandResolver.Resolve(-500, Bands), Is.EqualTo("hostile"));
            Assert.That(BandResolver.Resolve(-100, Bands), Is.EqualTo("neutral"), "A boundary value belongs to the band it opens.");
            Assert.That(BandResolver.Resolve(50, Bands), Is.EqualTo("neutral"));
            Assert.That(BandResolver.Resolve(200, Bands), Is.EqualTo("friendly"));
            Assert.That(BandResolver.Resolve(1000, Bands), Is.EqualTo("friendly"));
        }

        [Test]
        public void BandResolver_NoBands_ReturnsNull()
        {
            Assert.That(BandResolver.Resolve(0, null), Is.Null);
            Assert.That(BandResolver.Resolve(0, new List<StandingBand>()), Is.Null);
        }
    }
}
