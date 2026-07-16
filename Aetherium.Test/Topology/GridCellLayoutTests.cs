using System;
using NUnit.Framework;
using Aetherium.Model;
using Aetherium.Topology;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// The client-side layout math (docs/grid-topologies.md P6): GridCellLayout must place the
    /// relative cells the server streams so that screen adjacency matches topological adjacency —
    /// square identity, hex honeycomb stagger, triangle half-width cells with derived parity —
    /// and its continuous positions must mirror the server topologies' own planar embeddings.
    /// </summary>
    [TestFixture]
    public class GridCellLayoutTests
    {
        [Test]
        public void Square_IsTheIdentityLayout()
        {
            Assert.That(GridCellLayout.CellCharWidth("square"), Is.EqualTo(2));
            Assert.That(GridCellLayout.CellCharWidth(null), Is.EqualTo(2), "no topology field means square");
            Assert.That(GridCellLayout.CharColumnOffset("square", 3, -5), Is.EqualTo(6));
            Assert.That(GridCellLayout.RowStaggerChars("square", 7), Is.EqualTo(0));
            Assert.That(GridCellLayout.RelXForCellIndex("square", 10, -4, 10), Is.EqualTo(0));
            Assert.That(GridCellLayout.CellParity("square", 0, 1, 1), Is.Null);
            Assert.That(GridCellLayout.CellLayoutPosition("square", 3, -2), Is.EqualTo((3.0, -2.0)));
        }

        [Test]
        public void Hex_NeighborsLandHalfACellOver()
        {
            // Pointy-top axial: the two upward edges (+1,-1) and (0,-1) sit one character right
            // and left of the origin cell on the row above — the honeycomb.
            int origin = GridCellLayout.CharColumnOffset("hex", 0, 0);
            Assert.That(GridCellLayout.CharColumnOffset("hex", +1, -1) - origin, Is.EqualTo(+1));
            Assert.That(GridCellLayout.CharColumnOffset("hex", 0, -1) - origin, Is.EqualTo(-1));
            Assert.That(GridCellLayout.CharColumnOffset("hex", +1, 0) - origin, Is.EqualTo(+2), "east is one full cell");
        }

        [Test]
        public void Hex_StaggerAndCellIndexReconstructTheColumnFormula()
        {
            // The cell-driven loop (stagger + index mapping) must agree with the direct column
            // formula for every cell in a viewport-sized window, including negative rows.
            const int xoffset = 10;
            for (int relY = -6; relY <= 6; relY++)
            {
                int stagger = GridCellLayout.RowStaggerChars("hex", relY);
                Assert.That(stagger, Is.EqualTo(((relY % 2) + 2) % 2));
                for (int cellIndex = 0; cellIndex < 20; cellIndex++)
                {
                    int relX = GridCellLayout.RelXForCellIndex("hex", cellIndex, relY, xoffset);
                    int column = cellIndex * 2 + stagger;
                    int expected = 2 * xoffset + GridCellLayout.CharColumnOffset("hex", relX, relY);
                    Assert.That(column, Is.EqualTo(expected), $"cell {cellIndex} on row {relY}");
                }
            }
        }

        [Test]
        public void Triangle_CellsAreHalfWidthAndParityDerivesFromSelf()
        {
            Assert.That(GridCellLayout.CellCharWidth("tri"), Is.EqualTo(1));
            Assert.That(GridCellLayout.CharColumnOffset("tri", 5, 3), Is.EqualTo(5));
            Assert.That(GridCellLayout.RowStaggerChars("tri", 1), Is.EqualTo(0));

            // Perceiver on an up-cell (parity 0): every relX or relY step flips orientation.
            Assert.That(GridCellLayout.CellParity("tri", 0, 0, 0), Is.EqualTo(0));
            Assert.That(GridCellLayout.CellParity("tri", 0, 1, 0), Is.EqualTo(1));
            Assert.That(GridCellLayout.CellParity("tri", 0, 0, 1), Is.EqualTo(1));
            Assert.That(GridCellLayout.CellParity("tri", 0, -1, -1), Is.EqualTo(0), "negative deltas wrap correctly");
            Assert.That(GridCellLayout.CellParity("tri", 1, 0, 0), Is.EqualTo(1), "perceiver on a down-cell");
            Assert.That(GridCellLayout.CellParity("tri", null, 1, 0), Is.Null, "no parity from the server");
        }

        [Test]
        public void H3_LaysOutLikeHex()
        {
            Assert.That(GridCellLayout.CellCharWidth("h3"), Is.EqualTo(2));
            Assert.That(GridCellLayout.CharColumnOffset("h3", 2, 3), Is.EqualTo(GridCellLayout.CharColumnOffset("hex", 2, 3)));
            Assert.That(GridCellLayout.RowStaggerChars("h3", 1), Is.EqualTo(1));
        }

        [Test]
        public void ContinuousPositions_MirrorTheServerEmbeddings()
        {
            // The client's pixel-space layout must agree with the server's CellCenter embeddings,
            // shifted to the perceiver's own cell — otherwise client visuals and server geometry
            // (FOV cones, light falloff) would disagree about where a cell is.
            var hex = HexTopology.Instance;
            var hexSelf = new GridCoord(4, -2, 0);
            foreach (var (relX, relY) in new[] { (0, 0), (1, 0), (0, 1), (-3, 2), (2, -4) })
            {
                var expected = hex.Delta(hexSelf, new GridCoord(hexSelf.X + relX, hexSelf.Y + relY, 0));
                var actual = GridCellLayout.CellLayoutPosition("hex", relX, relY);
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-9), $"hex ({relX},{relY}) X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-9), $"hex ({relX},{relY}) Y");
            }

            var tri = TriangleTopology.Instance;
            var triSelf = new GridCoord(6, 2, 0); // parity 0 (up-cell)
            foreach (var (relX, relY) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (3, -2) })
            {
                var expected = tri.Delta(triSelf, new GridCoord(triSelf.X + relX, triSelf.Y + relY, 0));
                var actual = GridCellLayout.CellLayoutPosition("tri", relX, relY, selfCellParity: 0);
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-9), $"tri ({relX},{relY}) X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-9), $"tri ({relX},{relY}) Y");
            }
        }
    }
}
