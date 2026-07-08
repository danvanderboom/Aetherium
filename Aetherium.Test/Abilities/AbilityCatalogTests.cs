using NUnit.Framework;
using Aetherium.Server.Abilities;

namespace Aetherium.Test.Abilities
{
    /// <summary>Verifies "Ability Data Asset" (openspec/changes/add-abilities/specs/abilities/spec.md).</summary>
    [TestFixture]
    public class AbilityCatalogTests
    {
        [Test]
        public void Add_ThenTryGet_ReturnsTheSameAbility()
        {
            var catalog = new AbilityCatalog();
            var ability = new Ability("fireball", effects: System.Array.Empty<IAbilityEffect>(),
                resourcePoolTag: "mana", resourceCost: 20, castTime: 2, cooldown: 5, range: 8, targetShape: "projectile");

            Assert.That(catalog.Add(ability), Is.True);
            Assert.That(catalog.TryGet("fireball", out var found), Is.True);
            Assert.That(found!.ResourcePoolTag, Is.EqualTo("mana"));
            Assert.That(found.ResourceCost, Is.EqualTo(20));
            Assert.That(found.TargetShape, Is.EqualTo("projectile"));
        }

        [Test]
        public void Add_DuplicateId_IsRejected()
        {
            var catalog = new AbilityCatalog();
            catalog.Add(new Ability("fireball", effects: System.Array.Empty<IAbilityEffect>()));

            Assert.That(catalog.Add(new Ability("fireball", effects: System.Array.Empty<IAbilityEffect>())), Is.False);
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            var catalog = new AbilityCatalog();
            Assert.That(catalog.TryGet("does_not_exist", out _), Is.False);
        }
    }
}
