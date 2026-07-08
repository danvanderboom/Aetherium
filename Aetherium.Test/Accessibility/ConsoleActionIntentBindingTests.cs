extern alias Console;

using System;
using NUnit.Framework;
using ConsoleActionIntentBinding = Console::Aetherium.Input.ConsoleActionIntentBinding;

namespace Aetherium.Test.Accessibility
{
    /// <summary>
    /// Verifies "Console Input Translated To Action Intents"
    /// (openspec/changes/wire-accessibility-live/specs/accessibility-contract/spec.md): the real
    /// console keypresses that already correspond to a seeded <c>ActionIntent</c> resolve to its
    /// id; everything else (debug/meta keys, and combat, which is an implicit bump-attack rather
    /// than a distinct keypress) resolves to null.
    /// </summary>
    [TestFixture]
    public class ConsoleActionIntentBindingTests
    {
        [TestCase(ConsoleKey.UpArrow, "move")]
        [TestCase(ConsoleKey.W, "move")]
        [TestCase(ConsoleKey.DownArrow, "move")]
        [TestCase(ConsoleKey.S, "move")]
        [TestCase(ConsoleKey.LeftArrow, "move")]
        [TestCase(ConsoleKey.A, "move")]
        [TestCase(ConsoleKey.RightArrow, "move")]
        [TestCase(ConsoleKey.D, "move")]
        [TestCase(ConsoleKey.G, "pickup")]
        [TestCase(ConsoleKey.P, "drop")]
        [TestCase(ConsoleKey.O, "interact_open")]
        [TestCase(ConsoleKey.L, "interact_close")]
        public void Resolve_KeyWithCatalogEntry_ReturnsItsIntentId(ConsoleKey key, string expectedIntentId)
        {
            Assert.That(ConsoleActionIntentBinding.Resolve(key), Is.EqualTo(expectedIntentId));
        }

        [TestCase(ConsoleKey.Q)]   // fine rotation — debug/meta, outside the seed catalog
        [TestCase(ConsoleKey.R)]   // level change — debug/meta
        [TestCase(ConsoleKey.D1)]  // vision/lighting preset — debug/meta
        [TestCase(ConsoleKey.Escape)] // quit — meta, not a game action
        public void Resolve_KeyWithNoCatalogEntry_ReturnsNull(ConsoleKey key)
        {
            Assert.That(ConsoleActionIntentBinding.Resolve(key), Is.Null);
        }
    }
}
