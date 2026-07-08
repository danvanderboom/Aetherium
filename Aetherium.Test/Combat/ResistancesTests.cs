using NUnit.Framework;
using Aetherium.Server.Combat;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Verifies "Damage Packets" and "Per-Tag Damage Mitigation"
    /// (openspec/changes/deepen-combat-model/specs/combat/spec.md).
    /// </summary>
    [TestFixture]
    public class ResistancesTests
    {
        [Test]
        public void Mitigate_NoEntry_ReturnsFullAmount()
        {
            var resistances = new Resistances();
            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(10));
        }

        [Test]
        public void Mitigate_AppliesFlatThenPercent_ThenMinimumFloor()
        {
            var resistances = new Resistances();
            // 10 raw -> (10 - 2) flat = 8 -> * (1 - 0.5) percent = 4 -> floor at minimum 1 (no-op here)
            resistances.Set("fire", new ResistanceEntry(flat: 2, percent: 0.5, minimum: 1));

            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(4));
        }

        [Test]
        public void Mitigate_MinimumFloor_AppliesWhenPercentWouldGoLower()
        {
            var resistances = new Resistances();
            // (10 - 9) flat = 1 -> * (1 - 0.9) percent = 0.1 -> floored to minimum 2
            resistances.Set("fire", new ResistanceEntry(flat: 9, percent: 0.9, minimum: 2));

            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(2));
        }

        [Test]
        public void Mitigate_NeverExceedsOriginalAmount()
        {
            var resistances = new Resistances();
            // Minimum higher than the raw amount must not amplify damage.
            resistances.Set("fire", new ResistanceEntry(flat: 0, percent: 0, minimum: 999));

            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(10));
        }

        [Test]
        public void Mitigate_NeverGoesNegative()
        {
            var resistances = new Resistances();
            resistances.Set("fire", new ResistanceEntry(flat: 100, percent: 0, minimum: 0));

            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(0));
        }

        [Test]
        public void Mitigate_IsPerTag()
        {
            var resistances = new Resistances();
            resistances.Set("fire", new ResistanceEntry(flat: 5));

            Assert.That(resistances.Mitigate("fire", 10), Is.EqualTo(5));
            Assert.That(resistances.Mitigate("cold", 10), Is.EqualTo(10), "Untagged resistance must not affect other tags.");
        }

        [Test]
        public void DamageResolution_SumsAcrossMultipleComponents()
        {
            var resistances = new Resistances();
            resistances.Set("fire", new ResistanceEntry(flat: 2));

            var packet = new DamagePacket(new[]
            {
                new DamageComponent("fire", 10),
                new DamageComponent("cold", 4),
            });

            Assert.That(DamageResolution.ResolveTotal(packet, resistances), Is.EqualTo(8 + 4));
        }

        [Test]
        public void DamageResolution_NullResistances_AppliesNoMitigation()
        {
            var packet = DamagePacket.Single("fire", 7);
            Assert.That(DamageResolution.ResolveTotal(packet, null), Is.EqualTo(7));
        }
    }
}
