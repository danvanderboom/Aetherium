using System.Collections.Generic;
using System.Linq;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class CrossSectionBuilderTests
    {
        // Player at origin; content across bands -1/0/1 on the player's row, plus one off-row visual (y=1).
        private static PerceptionLite Slice()
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["0,0,0"]  = new VisualLite(new WorldLocationLite(0, 0, 0), "road", 1.0),
                    ["1,0,0"]  = new VisualLite(new WorldLocationLite(1, 0, 0), "road", 1.0),
                    ["-1,0,0"] = new VisualLite(new WorldLocationLite(-1, 0, 0), "stone", 1.0),
                    ["0,0,1"]  = new VisualLite(new WorldLocationLite(0, 0, 1), "metal", 1.0),
                    ["1,0,1"]  = new VisualLite(new WorldLocationLite(1, 0, 1), "metal", 1.0),
                    ["0,0,-1"] = new VisualLite(new WorldLocationLite(0, 0, -1), "track", 1.0),
                    ["2,1,0"]  = new VisualLite(new WorldLocationLite(2, 1, 0), "grass", 1.0), // off the player's row
                },
                TileTypes = new Dictionary<string, TileTypeLite>(),
            };
        }

        [Test]
        public void Build_OrdersBandsTopFirst()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);

            Assert.AreEqual(3, rows.Count, "Bands -1/0/1 intersect the row strip");
            Assert.AreEqual(1, rows[0].Band, "Highest band renders first (top)");
            Assert.AreEqual(-1, rows[rows.Count - 1].Band, "Lowest band renders last (bottom)");
            for (int i = 1; i < rows.Count; i++)
                Assert.Less(rows[i].Band, rows[i - 1].Band, "Bands strictly descend");
        }

        [Test]
        public void Build_FocusRowPresentAndFlagged()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);

            var focusRows = rows.Where(r => r.IsFocus).ToList();
            Assert.AreEqual(1, focusRows.Count, "Exactly one focus row");
            Assert.AreEqual(0, focusRows[0].Band, "Focus is the player's band");
        }

        [Test]
        public void Build_PlayerCellAtFocusCenter()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);
            var focus = rows.Single(r => r.IsFocus);

            var center = focus.Cells[focus.HalfWidth]; // dx = 0
            Assert.AreEqual(CrossSectionContent.Player, center.Content);
        }

        [Test]
        public void Build_ContentCellsCarryTileTypeId()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);
            var focus = rows.Single(r => r.IsFocus);

            var east = focus.Cells[focus.HalfWidth + 1];  // dx = +1
            var west = focus.Cells[focus.HalfWidth - 1];  // dx = -1
            Assert.AreEqual(CrossSectionContent.Content, east.Content);
            Assert.AreEqual("road", east.TileTypeId);
            Assert.AreEqual("stone", west.TileTypeId);
        }

        [Test]
        public void Build_EmptyWhereNoContent()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);
            var focus = rows.Single(r => r.IsFocus);

            var far = focus.Cells[focus.HalfWidth + 2]; // dx = +2, nothing there
            Assert.AreEqual(CrossSectionContent.Empty, far.Content);
        }

        [Test]
        public void Build_SliceExcludesOtherRows()
        {
            var rows = CrossSectionBuilder.Build(Slice(), halfWidth: 2);

            // The off-row grass tile (y = 1) must not introduce a band or appear in any cell.
            Assert.IsFalse(rows.Any(r => r.Cells.Any(c => c.TileTypeId == "grass")),
                "Content off the player's row is excluded from the elevation slice");
        }

        [Test]
        public void Build_NullOrNegativeHalfWidth_ReturnsEmpty()
        {
            Assert.IsEmpty(CrossSectionBuilder.Build(null, 2));
            Assert.IsEmpty(CrossSectionBuilder.Build(Slice(), -1));
        }
    }
}
