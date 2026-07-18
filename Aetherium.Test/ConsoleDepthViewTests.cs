extern alias Console;
using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Model;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 2 of add-adaptive-depth-visualization: console depth composite + level ribbon. Verifies the
    /// per-screen-cell composite over the perception slab (focus band wins, open focus cells fall through to what
    /// shows beneath, the topmost drawable band wins otherwise, overhead entities render as silhouettes) and the
    /// HUD level ribbon (occupied bands top-first with the focus band marked). Rendering is checked through the
    /// deterministic ASCII capture; single-band worlds render exactly as before.
    /// </summary>
    public class ConsoleDepthViewTests
    {
        // 20x10 content, symbolWidth 2 → 10x10 cells, player centred at cell (5,5); relative (rx,ry) → Tiles[ry+5][rx+5].
        private static ClientConsoleMapView MakeView() =>
            new ClientConsoleMapView(new Point(0, 0), new Size(20, 10), hasFrame: false);

        private static VisualDto Terrain(int x, int y, int z, string mapChar)
        {
            return new VisualDto
            {
                Location = new WorldLocationDto(x, y, z),
                LightLevel = 1.0,
                Terrain = new TileTypeDto { Name = "t" + mapChar, Settings = new Dictionary<string, string> { ["MapCharacter"] = mapChar } }
            };
        }

        private static VisualDto Open(int x, int y, int z) =>
            new VisualDto { Location = new WorldLocationDto(x, y, z), LightLevel = 1.0 }; // no terrain, nothing seen

        private static VisualDto Creature(int x, int y, int z)
        {
            var v = new VisualDto { Location = new WorldLocationDto(x, y, z), LightLevel = 1.0 };
            v.ThingsSeen[Aetherium.Model.VisualType.Character] = 1;
            return v;
        }

        private static ClientConsoleMapView ViewWith(params VisualDto[] visuals)
        {
            var p = new PerceptionDto { PlayerLocation = new WorldLocationDto(0, 0, 0) };
            p.TileTypes["Player"] = new TileTypeDto { Name = "Player", Settings = new Dictionary<string, string> { ["MapCharacter"] = "@" } };
            foreach (var v in visuals)
                p.Visuals[$"{v.Location.X},{v.Location.Y},{v.Location.Z}"] = v;

            var view = MakeView();
            view.Perception = p;
            view.WorldLocation = p.PlayerLocation;
            return view;
        }

        private static string Cell(Console::Aetherium.Monitoring.AsciiMapData map, int rx, int ry) => map.Tiles[ry + 5][rx + 5];

        [Test]
        public void FocusBand_WinsOver_BandBelow()
        {
            var view = ViewWith(Terrain(2, 0, 0, "."), Terrain(2, 0, -2, "="));
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("..", Cell(map, 2, 0), "The focus band renders over a band below it.");
        }

        [Test]
        public void OpenFocus_FallsThroughTo_BandBelow()
        {
            var view = ViewWith(Open(3, 0, 0), Terrain(3, 0, -2, "="));
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("==", Cell(map, 3, 0), "An open focus cell (grate) shows the level below.");
        }

        [Test]
        public void OverheadEntity_RendersAs_Silhouette()
        {
            var view = ViewWith(Open(4, 0, 0), Creature(4, 0, 2)); // flyer two bands up, clear focus
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("^^", Cell(map, 4, 0), "An overhead flyer with no terrain renders as a silhouette.");
        }

        [Test]
        public void TopmostBand_Wins_AmongBandsAbove()
        {
            var view = ViewWith(Open(2, 2, 0), Terrain(2, 2, 1, "#"), Terrain(2, 2, 3, "%"));
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("%%", Cell(map, 2, 2), "The topmost deck occludes lower ones from above.");
        }

        [Test]
        public void SingleBand_RendersFocusTerrain_Unchanged()
        {
            var view = ViewWith(Terrain(1, 0, 0, "."));
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("..", Cell(map, 1, 0), "A single-band world renders its focus terrain as before.");
            Assert.AreEqual("  ", Cell(map, -3, 0), "A column with no visual is blank.");
        }

        [Test]
        public void Player_StaysCentered()
        {
            var view = ViewWith(Terrain(0, 0, 0, "."), Terrain(2, 0, 3, "%"));
            var map = view.CaptureRenderedFrame();
            Assert.AreEqual("@@", Cell(map, 0, 0), "The player remains centred during depth compositing.");
        }

        [Test]
        public void LevelRibbon_ListsOccupiedBands_TopFirst_WithFocusMarked()
        {
            var view = ViewWith(Terrain(2, 0, 2, "^"), Terrain(0, 0, 0, "."), Terrain(3, 0, -1, "="));
            var ribbon = view.BuildLevelRibbon();

            CollectionAssert.AreEqual(
                new List<(int, bool)> { (2, false), (0, true), (-1, false) },
                ribbon,
                "The ribbon lists occupied bands top-first, with the focus band marked.");
        }

        [Test]
        public void LevelRibbon_AlwaysIncludesFocusBand()
        {
            var view = ViewWith(Terrain(2, 0, 2, "^"), Terrain(3, 0, -2, "=")); // nothing at the focus band itself
            var ribbon = view.BuildLevelRibbon();

            Assert.Contains((0, true), ribbon, "The focus band is always part of the stack, even when empty.");
        }

        // --- Cross-section / elevation view (Section 4) ---

        [Test]
        public void CrossSection_StacksInterchangeBands_TopFirst_WithFocusMarked()
        {
            // A three-level interchange in the player's column: a viaduct deck above, the street at focus, a
            // subway platform below.
            var view = ViewWith(
                Terrain(0, 0, 4, "="),  // viaduct deck (+4)
                Terrain(0, 0, 0, "."),  // street (focus)
                Terrain(0, 0, -2, "_")); // subway platform (-2)

            var rows = view.BuildCrossSection(halfWidth: 5);

            Assert.AreEqual(3, rows.Count, "One row per occupied band.");
            Assert.AreEqual(4, rows[0].band, "Top band first.");
            Assert.IsFalse(rows[0].isFocus);
            Assert.AreEqual(0, rows[1].band);
            Assert.IsTrue(rows[1].isFocus, "The focus band is marked.");
            Assert.AreEqual(-2, rows[2].band, "Bottom band last.");

            StringAssert.Contains("==", rows[0].cells, "The viaduct deck appears on the top band.");
            StringAssert.Contains("@@", rows[1].cells, "The player is marked on the focus band.");
            StringAssert.Contains("__", rows[2].cells, "The subway platform appears on the bottom band.");
        }

        [Test]
        public void CrossSection_DoesNotRequireFov_ShowsSlabContentDirectly()
        {
            // A structure two cells east on an off-focus band still appears in the elevation (no per-tile FOV gate).
            var view = ViewWith(Terrain(2, 0, 3, "#"));
            var rows = view.BuildCrossSection(halfWidth: 5);

            var deck = rows.Find(r => r.band == 3);
            StringAssert.Contains("##", deck.cells, "Off-axis slab content is drawn schematically.");
        }

        [Test]
        public void CrossSection_AlwaysIncludesFocusBand_WithPlayer()
        {
            // Only off-focus bands have content; the focus band (with the player) is still part of the elevation.
            var view = ViewWith(Terrain(0, 0, 4, "="), Terrain(0, 0, -2, "_"));
            var rows = view.BuildCrossSection(halfWidth: 5);

            var focus = rows.Find(r => r.isFocus);
            Assert.AreEqual(0, focus.band, "The focus band is present even when it holds no terrain.");
            StringAssert.Contains("@@", focus.cells, "The player anchors the focus band of the elevation.");
        }
    }
}
