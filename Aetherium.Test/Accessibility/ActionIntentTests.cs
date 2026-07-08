using NUnit.Framework;
using Aetherium.Model.Accessibility;

namespace Aetherium.Test.Accessibility
{
    /// <summary>Verifies "Action Intent Abstraction" (openspec/changes/add-accessibility-contract/specs/accessibility-contract/spec.md).</summary>
    [TestFixture]
    public class ActionIntentTests
    {
        [Test]
        public void Add_ThenTryGet_ReturnsTheSameIntent()
        {
            var catalog = new ActionIntentCatalog();
            catalog.Add(new ActionIntent("jump", "Jump over an obstacle"));

            Assert.That(catalog.TryGet("jump", out var found), Is.True);
            Assert.That(found!.Description, Is.EqualTo("Jump over an obstacle"));
        }

        [Test]
        public void Add_DuplicateId_IsRejected()
        {
            var catalog = new ActionIntentCatalog();
            catalog.Add(new ActionIntent("jump", "First"));

            Assert.That(catalog.Add(new ActionIntent("jump", "Second")), Is.False);
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            var catalog = new ActionIntentCatalog();
            Assert.That(catalog.TryGet("does_not_exist", out _), Is.False);
        }

        [Test]
        public void DefaultActionIntents_CoversRealExistingGameActions()
        {
            var catalog = DefaultActionIntents.Build();

            Assert.That(catalog.TryGet("move", out _), Is.True);
            Assert.That(catalog.TryGet("attack", out _), Is.True);
            Assert.That(catalog.TryGet("pickup", out _), Is.True);
            Assert.That(catalog.TryGet("drop", out _), Is.True);
            Assert.That(catalog.TryGet("use_item", out _), Is.True);
        }
    }
}
