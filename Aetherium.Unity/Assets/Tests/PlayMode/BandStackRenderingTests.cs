using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    /// <summary>
    /// PlayMode tests for the multi-band tilemap stack (Task 3.2/3.5). These run in
    /// PlayMode so MonoBehaviour Awake fires on AddComponent and the child band
    /// tilemaps actually render.
    /// </summary>
    public class BandStackRenderingTests
    {
        private GameObject? host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
                host = null;
            }
        }

        private BandStackRenderer NewRenderer()
        {
            host = new GameObject("BandStack");
            return host.AddComponent<BandStackRenderer>();
        }

        private static PerceptionLite ThreeBandSlab(int focusZ = 0)
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, focusZ),
                PlayerHeading = WorldDirectionLite.North,
                HeadingDegrees = 0,
                VisibleBounds = new RectangleLite(-3, -3, 7, 7),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["0,0,0"]  = new VisualLite(new WorldLocationLite(0, 0, 0), "road", 1.0),
                    ["1,0,0"]  = new VisualLite(new WorldLocationLite(1, 0, 0), "road", 0.9),
                    ["0,0,-1"] = new VisualLite(new WorldLocationLite(0, 0, -1), "track", 0.4),
                    ["0,1,-1"] = new VisualLite(new WorldLocationLite(0, 1, -1), "track", 0.4),
                    ["1,0,1"]  = new VisualLite(new WorldLocationLite(1, 0, 1), "metal", 0.8),
                },
                TileTypes = new Dictionary<string, TileTypeLite>
                {
                    ["road"]  = new TileTypeLite("Road", null),
                    ["track"] = new TileTypeLite("Track", null),
                    ["metal"] = new TileTypeLite("Metal", null),
                },
            };
        }

        [Test]
        public void RenderPerception_CreatesOneTilemapPerBand()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab());

            Assert.IsNotNull(stack.GetBand(-1), "Subway band should exist");
            Assert.IsNotNull(stack.GetBand(0), "Focus band should exist");
            Assert.IsNotNull(stack.GetBand(1), "Viaduct band should exist");
            Assert.AreEqual(3, stack.ActiveBandCount, "Exactly three bands should have tiles");
        }

        [Test]
        public void RenderPerception_FocusBandOpaque_OffFocusFadedAndSymmetric()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab(focusZ: 0));

            Assert.IsTrue(stack.TryGetBandAlpha(0, out var focusA));
            Assert.IsTrue(stack.TryGetBandAlpha(1, out var upA));
            Assert.IsTrue(stack.TryGetBandAlpha(-1, out var downA));

            Assert.AreEqual(1f, focusA, 1e-4f, "Focus band must be opaque");
            Assert.Less(upA, focusA, "Band above focus must be faded");
            Assert.Less(downA, focusA, "Band below focus must be faded");
            Assert.AreEqual(upA, downA, 1e-4f, "Bands one step away must fade equally");
        }

        [Test]
        public void RenderPerception_SortsBandsByAltitude()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab());

            int below = stack.GetBand(-1)!.GetSortingOrder();
            int focus = stack.GetBand(0)!.GetSortingOrder();
            int above = stack.GetBand(1)!.GetSortingOrder();

            Assert.Less(below, focus);
            Assert.Less(focus, above);
        }

        [Test]
        public void RenderPerception_ChangingFocus_ReassignsOpacity()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab(focusZ: 0));
            // Player climbs the stairs to +1: focus follows, opacities reassign.
            stack.RenderPerception(ThreeBandSlab(focusZ: 1));

            Assert.AreEqual(1, stack.FocusZ);
            Assert.IsTrue(stack.TryGetBandAlpha(1, out var newFocusA));
            Assert.IsTrue(stack.TryGetBandAlpha(0, out var oldFocusA));

            Assert.AreEqual(1f, newFocusA, 1e-4f, "New focus band must become opaque");
            Assert.Less(oldFocusA, 1f, "Old focus band must fade once it is off-focus");
        }

        [Test]
        public void RenderPerception_VanishedBand_IsCleared()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab());
            Assert.Greater(stack.GetBand(1)!.GetRenderedTileCount(), 0, "Viaduct band starts with tiles");

            // Next frame has no cells at z = 1.
            var flat = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                VisibleBounds = new RectangleLite(-3, -3, 7, 7),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["0,0,0"] = new VisualLite(new WorldLocationLite(0, 0, 0), "road", 1.0),
                },
                TileTypes = new Dictionary<string, TileTypeLite>
                {
                    ["road"] = new TileTypeLite("Road", null),
                },
            };
            stack.RenderPerception(flat);

            Assert.AreEqual(0, stack.GetBand(1)!.GetRenderedTileCount(), "Emptied band should be cleared");
            Assert.Greater(stack.GetBand(0)!.GetRenderedTileCount(), 0, "Focus band still populated");
        }

        [Test]
        public void RenderPerception_TileIsThemedByType()
        {
            var stack = NewRenderer();
            stack.RenderPerception(ThreeBandSlab());

            // The focus band wrote a colored Tile for "road"; verify theming reached it.
            var expected = TileTheme.ColorFor("Road");
            Assert.IsTrue(stack.GetBand(0)!.TryGetTileColor(0, 0, out var actual),
                "Focus tile should exist at grid (0,0)");
            Assert.AreEqual(expected, actual, "Rendered tile must carry its themed color");
        }
    }
}
