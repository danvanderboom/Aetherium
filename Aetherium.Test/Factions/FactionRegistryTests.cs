using NUnit.Framework;
using Aetherium.Server.Factions;

namespace Aetherium.Test.Factions
{
    /// <summary>Verifies "Faction Registry" (openspec/changes/add-factions/specs/factions/spec.md).</summary>
    [TestFixture]
    public class FactionRegistryTests
    {
        [Test]
        public void Add_ThenTryGet_ReturnsTheSameFaction()
        {
            var registry = new FactionRegistry();
            var faction = new Faction("merchants_guild", "Merchants' Guild", new FactionDoctrine(), tags: new[] { "commerce" });

            Assert.That(registry.Add(faction), Is.True);
            Assert.That(registry.TryGet("merchants_guild", out var found), Is.True);
            Assert.That(found!.Name, Is.EqualTo("Merchants' Guild"));
            Assert.That(found.Tags, Does.Contain("commerce"));
        }

        [Test]
        public void Add_DuplicateId_IsRejected()
        {
            var registry = new FactionRegistry();
            registry.Add(new Faction("guild", "Guild A", new FactionDoctrine()));

            Assert.That(registry.Add(new Faction("guild", "Guild B", new FactionDoctrine())), Is.False);
        }

        [Test]
        public void Faction_AddMember_ThenIsMember_ReflectsMembership()
        {
            var faction = new Faction("guild", "Guild", new FactionDoctrine());

            Assert.That(faction.IsMember("npc-1"), Is.False);
            faction.AddMember("npc-1");
            Assert.That(faction.IsMember("npc-1"), Is.True);
        }
    }
}
