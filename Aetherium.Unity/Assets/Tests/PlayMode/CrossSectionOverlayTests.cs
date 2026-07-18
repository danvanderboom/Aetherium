using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class CrossSectionOverlayTests
    {
        private GameObject? host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) { Object.DestroyImmediate(host); host = null; }
        }

        private CrossSectionOverlay NewOverlay()
        {
            host = new GameObject("CrossSection");
            return host.AddComponent<CrossSectionOverlay>();
        }

        private static PerceptionLite Slice()
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["1,0,0"]  = new VisualLite(new WorldLocationLite(1, 0, 0), "road", 1.0),
                    ["0,0,1"]  = new VisualLite(new WorldLocationLite(0, 0, 1), "metal", 1.0),
                    ["0,0,-1"] = new VisualLite(new WorldLocationLite(0, 0, -1), "track", 1.0),
                },
                TileTypes = new Dictionary<string, TileTypeLite>(),
            };
        }

        [Test]
        public void Render_DrawsSchematicCells()
        {
            var overlay = NewOverlay();
            overlay.Render(Slice(), halfWidth: 3);

            Assert.Greater(overlay.RenderedCellCount, 0, "Schematic should draw at least the player + content cells");
        }

        [Test]
        public void Render_PlayerCellUsesReservedMarkerColor()
        {
            var overlay = NewOverlay();
            overlay.Render(Slice(), halfWidth: 3);

            // Player is placed at (dx=0, band=focus=0) -> grid cell (0,0).
            Assert.IsTrue(overlay.Renderer!.TryGetTileColor(0, 0, out var color), "Player cell should be drawn");
            Assert.AreEqual(TileTheme.ColorFor("Player"), color, "Player uses the reserved marker color");
        }

        [Test]
        public void Render_ContentCellUsesTerrainColor()
        {
            var overlay = NewOverlay();
            overlay.Render(Slice(), halfWidth: 3);

            // The +1 band metal tile is placed at (dx=0, band=1) -> grid cell (0,1).
            Assert.IsTrue(overlay.Renderer!.TryGetTileColor(0, 1, out var color), "Band +1 content cell should be drawn");
            Assert.AreEqual(TileTheme.ColorFor("metal"), color);
        }

        [Test]
        public void Show_TogglesVisibility()
        {
            var overlay = NewOverlay();
            overlay.Render(Slice(), halfWidth: 3);

            overlay.Show(false);
            Assert.IsFalse(overlay.IsShowing);

            overlay.Show(true);
            Assert.IsTrue(overlay.IsShowing);
        }
    }
}
