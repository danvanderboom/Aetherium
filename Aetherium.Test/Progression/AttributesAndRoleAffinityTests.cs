using NUnit.Framework;
using Aetherium.Server.Progression;

namespace Aetherium.Test.Progression
{
    /// <summary>Verifies "Per-Campaign Attributes" and "Role Affinity"
    /// (openspec/changes/add-character-progression/specs/character-progression/spec.md).</summary>
    [TestFixture]
    public class AttributesAndRoleAffinityTests
    {
        [Test]
        public void Attributes_UnsetName_ReturnsDefaultValue()
        {
            var attributes = new Attributes();
            Assert.That(attributes.Get("strength"), Is.EqualTo(0));
            Assert.That(attributes.Get("strength", defaultValue: 10), Is.EqualTo(10));
            Assert.That(attributes.Has("strength"), Is.False);
        }

        [Test]
        public void Attributes_SetAndGet_ArbitraryCampaignDefinedName()
        {
            var attributes = new Attributes();
            attributes.Set("hacking", 7.5); // A sci-fi-campaign attribute, not engine-known.

            Assert.That(attributes.Get("hacking"), Is.EqualTo(7.5));
            Assert.That(attributes.Has("hacking"), Is.True);
        }

        [Test]
        public void Attributes_EngineDefaults_AreNamedConstants()
        {
            var attributes = new Attributes();
            attributes.Set(Attributes.Vitality, 100);
            attributes.Set(Attributes.Speed, 1.5);

            Assert.That(attributes.Get(Attributes.Vitality), Is.EqualTo(100));
            Assert.That(attributes.Get(Attributes.Speed), Is.EqualTo(1.5));
        }

        [Test]
        public void RoleAffinity_UnsetTag_ReturnsDefaultValue()
        {
            var affinity = new RoleAffinity();
            Assert.That(affinity.Get("tank"), Is.EqualTo(0));
        }

        [Test]
        public void RoleAffinity_FreeformBuild_HasNoWeights()
        {
            var affinity = new RoleAffinity();
            Assert.That(affinity.Get("tank"), Is.EqualTo(0));
            Assert.That(affinity.Get("healer"), Is.EqualTo(0));
        }

        [Test]
        public void RoleAffinity_FixedArchetype_HasDominantWeight()
        {
            var affinity = new RoleAffinity();
            affinity.Set("tank", 0.9);
            affinity.Set("healer", 0.1);

            Assert.That(affinity.Get("tank"), Is.GreaterThan(affinity.Get("healer")));
        }
    }
}
