using NUnit.Framework;
using Aetherium.Server.Factions;

namespace Aetherium.Test.Factions
{
    /// <summary>Verifies "Reputation Ledger" (openspec/changes/add-factions/specs/factions/spec.md).</summary>
    [TestFixture]
    public class ReputationLedgerTests
    {
        [Test]
        public void ApplyAction_UsesDoctrineDelta_ToAdjustStanding()
        {
            var doctrine = new FactionDoctrine();
            doctrine.SetDelta("peaceful_resolution", 10);
            var faction = new Faction("pacifists", "The Pacifists", doctrine);
            var ledger = new ReputationLedger();

            var reputation = ledger.ApplyAction(faction, "peaceful_resolution");

            Assert.That(reputation.Standing, Is.EqualTo(10));
        }

        [Test]
        public void ApplyAction_DifferentFactionDoctrines_ReactDifferently_ToTheSameAction()
        {
            var pacifistDoctrine = new FactionDoctrine();
            pacifistDoctrine.SetDelta("violence", -20);
            var militantDoctrine = new FactionDoctrine();
            militantDoctrine.SetDelta("violence", 15);

            var pacifists = new Faction("pacifists", "Pacifists", pacifistDoctrine);
            var militants = new Faction("militants", "Militants", militantDoctrine);
            var ledger = new ReputationLedger();

            ledger.ApplyAction(pacifists, "violence");
            ledger.ApplyAction(militants, "violence");

            Assert.That(ledger.ByFaction["pacifists"].Standing, Is.EqualTo(-20));
            Assert.That(ledger.ByFaction["militants"].Standing, Is.EqualTo(15));
        }

        [Test]
        public void ApplyAction_UnknownActionTag_AppliesZeroDelta()
        {
            var faction = new Faction("guild", "Guild", new FactionDoctrine());
            var ledger = new ReputationLedger();

            var reputation = ledger.ApplyAction(faction, "some_untracked_action");

            Assert.That(reputation.Standing, Is.EqualTo(0));
        }

        [Test]
        public void Standing_ClampsAtMaximum()
        {
            var doctrine = new FactionDoctrine();
            doctrine.SetDelta("big_favor", 5000);
            var faction = new Faction("guild", "Guild", doctrine);
            var ledger = new ReputationLedger();

            var reputation = ledger.ApplyAction(faction, "big_favor");

            Assert.That(reputation.Standing, Is.EqualTo(Reputation.MaxStanding));
        }

        [Test]
        public void Standing_ClampsAtMinimum()
        {
            var doctrine = new FactionDoctrine();
            doctrine.SetDelta("atrocity", -5000);
            var faction = new Faction("guild", "Guild", doctrine);
            var ledger = new ReputationLedger();

            var reputation = ledger.ApplyAction(faction, "atrocity");

            Assert.That(reputation.Standing, Is.EqualTo(Reputation.MinStanding));
        }

        [Test]
        public void ApplyAction_Repeated_Accumulates()
        {
            var doctrine = new FactionDoctrine();
            doctrine.SetDelta("small_favor", 5);
            var faction = new Faction("guild", "Guild", doctrine);
            var ledger = new ReputationLedger();

            ledger.ApplyAction(faction, "small_favor");
            ledger.ApplyAction(faction, "small_favor");
            var reputation = ledger.ApplyAction(faction, "small_favor");

            Assert.That(reputation.Standing, Is.EqualTo(15));
        }
    }
}
