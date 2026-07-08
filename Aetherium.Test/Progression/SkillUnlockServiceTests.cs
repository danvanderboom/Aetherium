using NUnit.Framework;
using Aetherium.Server.Progression;

namespace Aetherium.Test.Progression
{
    /// <summary>Verifies "Skill Prerequisite Gating"
    /// (openspec/changes/add-character-progression/specs/character-progression/spec.md).</summary>
    [TestFixture]
    public class SkillUnlockServiceTests
    {
        [Test]
        public void TryUnlock_RootSkill_NoPrerequisites_Succeeds()
        {
            var catalog = new SkillCatalog();
            catalog.Add(new SkillDefinition("basic_strike", "A basic attack skill."));
            var unlocked = new UnlockedSkills();

            var result = new SkillUnlockService().TryUnlock(unlocked, catalog, "basic_strike");

            Assert.That(result, Is.EqualTo(SkillUnlockResult.Unlocked));
            Assert.That(unlocked.Has("basic_strike"), Is.True);
        }

        [Test]
        public void TryUnlock_UnknownSkill_Fails()
        {
            var catalog = new SkillCatalog();
            var unlocked = new UnlockedSkills();

            var result = new SkillUnlockService().TryUnlock(unlocked, catalog, "does_not_exist");

            Assert.That(result, Is.EqualTo(SkillUnlockResult.UnknownSkill));
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_Fails()
        {
            var catalog = new SkillCatalog();
            catalog.Add(new SkillDefinition("basic_strike", "A basic attack skill."));
            var unlocked = new UnlockedSkills();
            new SkillUnlockService().TryUnlock(unlocked, catalog, "basic_strike");

            var result = new SkillUnlockService().TryUnlock(unlocked, catalog, "basic_strike");

            Assert.That(result, Is.EqualTo(SkillUnlockResult.AlreadyUnlocked));
        }

        [Test]
        public void TryUnlock_MissingPrerequisite_Fails()
        {
            var catalog = new SkillCatalog();
            catalog.Add(new SkillDefinition("basic_strike", "Root skill."));
            catalog.Add(new SkillDefinition("power_strike", "Needs basic_strike first.",
                prerequisites: new[] { "basic_strike" }));
            var unlocked = new UnlockedSkills();

            var result = new SkillUnlockService().TryUnlock(unlocked, catalog, "power_strike");

            Assert.That(result, Is.EqualTo(SkillUnlockResult.PrerequisitesNotMet));
            Assert.That(unlocked.Has("power_strike"), Is.False);
        }

        [Test]
        public void TryUnlock_PrerequisiteMet_Succeeds()
        {
            var catalog = new SkillCatalog();
            catalog.Add(new SkillDefinition("basic_strike", "Root skill."));
            catalog.Add(new SkillDefinition("power_strike", "Needs basic_strike first.",
                prerequisites: new[] { "basic_strike" }));
            var unlocked = new UnlockedSkills();
            new SkillUnlockService().TryUnlock(unlocked, catalog, "basic_strike");

            var result = new SkillUnlockService().TryUnlock(unlocked, catalog, "power_strike");

            Assert.That(result, Is.EqualTo(SkillUnlockResult.Unlocked));
            Assert.That(unlocked.Has("power_strike"), Is.True);
        }

        [Test]
        public void TryUnlock_MultiplePrerequisites_AllMustBeMet()
        {
            var catalog = new SkillCatalog();
            catalog.Add(new SkillDefinition("a", "Root A."));
            catalog.Add(new SkillDefinition("b", "Root B."));
            catalog.Add(new SkillDefinition("combo", "Needs both A and B.", prerequisites: new[] { "a", "b" }));
            var unlocked = new UnlockedSkills();
            new SkillUnlockService().TryUnlock(unlocked, catalog, "a");

            var stillBlocked = new SkillUnlockService().TryUnlock(unlocked, catalog, "combo");
            Assert.That(stillBlocked, Is.EqualTo(SkillUnlockResult.PrerequisitesNotMet));

            new SkillUnlockService().TryUnlock(unlocked, catalog, "b");
            var nowUnlocked = new SkillUnlockService().TryUnlock(unlocked, catalog, "combo");
            Assert.That(nowUnlocked, Is.EqualTo(SkillUnlockResult.Unlocked));
        }
    }
}
