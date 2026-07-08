using System.Collections.Generic;
using Aetherium.Model.Accessibility;

namespace Aetherium.Accessibility
{
    /// <summary>
    /// The real <see cref="SemanticDistinction"/>s <c>ClientConsoleMapView</c> draws today
    /// (engine gap-analysis §4.13, wire-accessibility-live Phase 2). Registered so
    /// <see cref="ColorblindLintRule"/> has something real to check instead of the empty set
    /// Phase 1 left it with — see the CI-enforcement test that runs the rule against this set.
    /// </summary>
    public static class ConsoleRendererDistinctions
    {
        public static IReadOnlyList<SemanticDistinction> Build()
        {
            // Terrain (wall/floor/door/water/tree/torch/...): every world builder assigns a
            // distinct MapCharacter glyph per terrain type alongside its color.
            var terrain = new SemanticDistinction("terrain-type", "Which terrain type a map cell is");
            terrain.MarkEncodedBy(AccessibilityChannel.Color);
            terrain.MarkEncodedBy(AccessibilityChannel.Shape);

            // The player's own marker: '@' glyph, distinct from any other tile/character glyph.
            var player = new SemanticDistinction("player-marker", "The player's own position on the map");
            player.MarkEncodedBy(AccessibilityChannel.Color);
            player.MarkEncodedBy(AccessibilityChannel.Shape);

            // Which colored key (red/blue/green/yellow) an item is. Was color-only until this
            // slice; ClientConsoleMapView.ResolveKeyItemGlyph now gives each key color a distinct
            // first-letter glyph too.
            var itemKeyColor = new SemanticDistinction("item-key-color", "Which colored key an item is");
            itemKeyColor.MarkEncodedBy(AccessibilityChannel.Color);
            itemKeyColor.MarkEncodedBy(AccessibilityChannel.Shape);

            return new[] { terrain, player, itemKeyColor };
        }
    }
}
