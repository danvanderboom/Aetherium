using NUnit.Framework;
using Aetherium.Server.Factions;

namespace Aetherium.Test.Factions
{
    /// <summary>Verifies "Inter-Faction Disposition" (openspec/changes/add-factions/specs/factions/spec.md).</summary>
    [TestFixture]
    public class FactionRelationsTests
    {
        [Test]
        public void GetDisposition_UnsetPair_DefaultsToNeutral()
        {
            var relations = new FactionRelations();
            Assert.That(relations.GetDisposition("a", "b"), Is.EqualTo(FactionDisposition.Neutral));
        }

        [Test]
        public void SetDisposition_IsDirected_NotAutomaticallyMirrored()
        {
            var relations = new FactionRelations();
            relations.SetDisposition("vassals", "empire", FactionDisposition.Subordinate);

            Assert.That(relations.GetDisposition("vassals", "empire"), Is.EqualTo(FactionDisposition.Subordinate));
            Assert.That(relations.GetDisposition("empire", "vassals"), Is.EqualTo(FactionDisposition.Neutral),
                "Subordinate must not imply the reverse direction is also Subordinate.");
        }

        [Test]
        public void SetMutual_AppliesToBothDirections()
        {
            var relations = new FactionRelations();
            relations.SetMutual("guild_a", "guild_b", FactionDisposition.War);

            Assert.That(relations.GetDisposition("guild_a", "guild_b"), Is.EqualTo(FactionDisposition.War));
            Assert.That(relations.GetDisposition("guild_b", "guild_a"), Is.EqualTo(FactionDisposition.War));
        }

        [Test]
        public void SetDisposition_CanBeChangedOverTime()
        {
            var relations = new FactionRelations();
            relations.SetMutual("a", "b", FactionDisposition.Cold);
            Assert.That(relations.GetDisposition("a", "b"), Is.EqualTo(FactionDisposition.Cold));

            relations.SetMutual("a", "b", FactionDisposition.Ally);
            Assert.That(relations.GetDisposition("a", "b"), Is.EqualTo(FactionDisposition.Ally));
        }
    }
}
