using NUnit.Framework;
using Aetherium.Server.Combat;

namespace Aetherium.Test.Combat
{
    /// <summary>Verifies "Threat Ledger" (openspec/changes/deepen-combat-model/specs/combat/spec.md).</summary>
    [TestFixture]
    public class ThreatTableTests
    {
        [Test]
        public void GetTopThreat_EmptyLedger_ReturnsNull()
        {
            var table = new ThreatTable();
            Assert.That(table.GetTopThreat(), Is.Null);
        }

        [Test]
        public void AddThreat_Accumulates_PerAttacker()
        {
            var table = new ThreatTable();
            table.AddThreat("attacker-1", 5);
            table.AddThreat("attacker-1", 3);

            Assert.That(table.ThreatByAttacker["attacker-1"], Is.EqualTo(8));
        }

        [Test]
        public void GetTopThreat_ReturnsHighestContributor()
        {
            var table = new ThreatTable();
            table.AddThreat("attacker-1", 5);
            table.AddThreat("attacker-2", 12);
            table.AddThreat("attacker-3", 7);

            Assert.That(table.GetTopThreat(), Is.EqualTo("attacker-2"));
        }
    }
}
