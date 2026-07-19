using System.Collections.Generic;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class MarchingSquaresTests
    {
        private static HashSet<Vector2Int> Corners(List<Vector2Int> loop) => new HashSet<Vector2Int>(loop);

        [Test]
        public void SingleCell_ProducesUnitSquareLoop()
        {
            var loops = MarchingSquares.TraceLoops(new[] { (0, 0) });
            Assert.AreEqual(1, loops.Count);
            Assert.AreEqual(4, loops[0].Count);
            Assert.IsTrue(Corners(loops[0]).SetEquals(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(0, 1),
            }));
        }

        [Test]
        public void TwoByTwoBlock_CollapsesToOuterSquare()
        {
            var loops = MarchingSquares.TraceLoops(new[] { (0, 0), (1, 0), (0, 1), (1, 1) });
            Assert.AreEqual(1, loops.Count);
            Assert.AreEqual(4, loops[0].Count, "collinear edge midpoints should be simplified away");
            Assert.IsTrue(Corners(loops[0]).SetEquals(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(2, 0),
                new Vector2Int(2, 2), new Vector2Int(0, 2),
            }));
        }

        [Test]
        public void LShape_HasSixCorners()
        {
            var loops = MarchingSquares.TraceLoops(new[] { (0, 0), (1, 0), (0, 1) });
            Assert.AreEqual(1, loops.Count);
            Assert.AreEqual(6, loops[0].Count);
        }

        [Test]
        public void RingWithHole_ProducesOuterAndInnerLoops()
        {
            // 3x3 block minus the centre cell (1,1) -> outer loop + a square hole loop.
            var cells = new List<(int, int)>();
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    if (!(x == 1 && y == 1))
                        cells.Add((x, y));

            var loops = MarchingSquares.TraceLoops(cells);
            Assert.AreEqual(2, loops.Count);

            var sizes = new List<int> { loops[0].Count, loops[1].Count };
            sizes.Sort();
            Assert.AreEqual(new List<int> { 4, 4 }, sizes, "outer square + square hole, each 4 corners");
        }

        [Test]
        public void DisjointRegions_ProduceSeparateLoops()
        {
            var loops = MarchingSquares.TraceLoops(new[] { (0, 0), (5, 5) });
            Assert.AreEqual(2, loops.Count);
        }

        [Test]
        public void Empty_ProducesNoLoops()
        {
            var loops = MarchingSquares.TraceLoops(new (int, int)[0]);
            Assert.AreEqual(0, loops.Count);
        }
    }
}
