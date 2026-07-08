extern alias Console;

using System;
using System.Linq;
using NUnit.Framework;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test.Accessibility
{
    /// <summary>
    /// Verifies "Colorblind-Safe Key Item Glyphs"
    /// (openspec/changes/wire-accessibility-live/specs/accessibility-contract/spec.md): a key
    /// item's color is no longer the only signal for which key it is — each key color now also
    /// renders a distinct glyph (its own first letter), fixing the "item-key-color" distinction
    /// that was color-only before this slice.
    /// </summary>
    [TestFixture]
    public class KeyItemGlyphTests
    {
        [TestCase("red", "R", ConsoleColor.Red)]
        [TestCase("blue", "B", ConsoleColor.Blue)]
        [TestCase("green", "G", ConsoleColor.Green)]
        [TestCase("yellow", "Y", ConsoleColor.Yellow)]
        public void ResolveKeyItemGlyph_KnownColor_ReturnsDistinctLetterAndColor(string keyId, string expectedIcon, ConsoleColor expectedColor)
        {
            var (icon, color) = ClientConsoleMapView.ResolveKeyItemGlyph(keyId);

            Assert.That(icon, Is.EqualTo(expectedIcon));
            Assert.That(color, Is.EqualTo(expectedColor));
        }

        [Test]
        public void ResolveKeyItemGlyph_DifferentColors_NeverShareAGlyph()
        {
            var icons = new[] { "red", "blue", "green", "yellow" }
                .Select(id => ClientConsoleMapView.ResolveKeyItemGlyph(id).Icon)
                .Distinct();

            Assert.That(icons.Count(), Is.EqualTo(4), "Each key color must render its own glyph, not just its own color.");
        }

        [Test]
        public void ResolveKeyItemGlyph_UnrecognizedColorName_StillGetsAGlyph()
        {
            var (icon, color) = ClientConsoleMapView.ResolveKeyItemGlyph("purple");

            Assert.That(icon, Is.EqualTo("P"), "Even a color the renderer can't map still gets a distinguishing glyph.");
            Assert.That(color, Is.EqualTo(ConsoleColor.White));
        }
    }
}
