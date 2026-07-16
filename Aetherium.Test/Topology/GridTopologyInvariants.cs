using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Topology;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// The 8-invariant property harness from docs/grid-topologies.md. Every topology —
    /// square today, hex/triangle later, and the deliberately-irregular PentagonishTopology
    /// CI mock (P3) — is run through <see cref="AssertAll"/> over a sample of cells. This is
    /// the permanent guard that no uniform-direction or global-plane assumption creeps back
    /// into the seam.
    /// </summary>
    public static class GridTopologyInvariants
    {
        /// <summary>Runs all eight invariants over a square sample of cells around the origin —
        /// the entry point for lattice topologies whose integer (x, y) are all valid cells.</summary>
        public static void AssertAll(IGridTopology topology, int extent = 3, int z = 0)
            => AssertAll(topology, Sample(extent, z), extent);

        /// <summary>
        /// Runs all eight invariants over an explicit cell set — the entry point for topologies
        /// (H3) whose cells are not an integer lattice, so the sample must be an actual set of
        /// valid cells (e.g. a gridDisk that includes at least one pentagon). Per-cell and
        /// pairwise checks range over the given cells; Range/BFS completeness is computed live
        /// from the topology at <paramref name="rangeRadius"/>, independent of the sample.
        /// </summary>
        public static void AssertAll(IGridTopology topology, IEnumerable<GridCoord> cells, int rangeRadius = 2)
        {
            var list = cells.ToList();

            foreach (var cell in list)
            {
                NeighborSymmetry(topology, cell);
                DirectionCountBounds(topology, cell);
                DistanceIsMetricLocally(topology, cell);
                HeadingConsistentWithDelta(topology, cell);
                ZOrthogonal(topology, cell);
            }

            // Pairwise metric + geometry over the sample (quadratic, but the sample is small).
            foreach (var a in list)
            foreach (var b in list)
            {
                DistanceMetricAxioms(topology, a, b);
                LineConnected(topology, a, b);
            }

            foreach (var center in list)
                RangeIsExactBall(topology, center, rangeRadius);
        }

        // 1. Neighbor symmetry: every edge has a reverse edge.
        private static void NeighborSymmetry(IGridTopology t, GridCoord cell)
        {
            foreach (var neighbor in t.Neighbors(cell))
                Assert.That(t.Neighbors(neighbor), Does.Contain(cell),
                    $"[{t.Name}] {neighbor} is a neighbor of {cell} but not vice-versa.");
        }

        // 5. 3 <= DirectionCount <= MaxDirectionCount <= 8; Steps agrees with DirectionCount.
        private static void DirectionCountBounds(IGridTopology t, GridCoord cell)
        {
            int n = t.DirectionCount(cell);
            Assert.That(n, Is.InRange(3, t.MaxDirectionCount), $"[{t.Name}] DirectionCount({cell}) out of bounds.");
            Assert.That(t.MaxDirectionCount, Is.LessThanOrEqualTo(8), $"[{t.Name}] MaxDirectionCount exceeds 8.");
            Assert.That(t.Steps(cell).Count(), Is.EqualTo(n), $"[{t.Name}] Steps({cell}) count disagrees with DirectionCount.");
            Assert.That(t.Neighbors(cell).Count(), Is.EqualTo(n), $"[{t.Name}] Neighbors({cell}) count disagrees with DirectionCount.");
        }

        // 2. Distance(a, b) == 1  iff  adjacent.
        private static void DistanceIsMetricLocally(IGridTopology t, GridCoord cell)
        {
            var neighbors = t.Neighbors(cell).ToHashSet();
            foreach (var n in neighbors)
                Assert.That(t.Distance(cell, n), Is.EqualTo(1),
                    $"[{t.Name}] adjacent cells {cell}->{n} must have Distance 1.");
        }

        // 7. Edge HeadingDegrees is consistent with Delta(cell, edge.Target).
        private static void HeadingConsistentWithDelta(IGridTopology t, GridCoord cell)
        {
            foreach (var step in t.Steps(cell))
            {
                var (dx, dy) = t.Delta(cell, step.Target);
                // Compass heading from a screen-axes vector: 0 = north (-Y), clockwise.
                double headingRad = System.Math.Atan2(dx, -dy);
                double headingDeg = headingRad * 180.0 / System.Math.PI;
                if (headingDeg < 0) headingDeg += 360;
                int diff = AngularDiff((int)System.Math.Round(headingDeg), step.HeadingDegrees);
                Assert.That(diff, Is.LessThanOrEqualTo(1),
                    $"[{t.Name}] edge heading {step.HeadingDegrees}° at {cell} disagrees with Delta-derived {headingDeg:F1}°.");
            }
        }

        // 8. Topology governs XY only — the Z axis is orthogonal and untouched.
        private static void ZOrthogonal(IGridTopology t, GridCoord cell)
        {
            foreach (var step in t.Steps(cell))
                Assert.That(step.Target.Z, Is.EqualTo(cell.Z),
                    $"[{t.Name}] a planar edge from {cell} changed Z.");
        }

        private static void DistanceMetricAxioms(IGridTopology t, GridCoord a, GridCoord b)
        {
            int dab = t.Distance(a, b);
            Assert.That(dab, Is.GreaterThanOrEqualTo(0), $"[{t.Name}] negative distance {a}->{b}.");
            Assert.That(dab, Is.EqualTo(t.Distance(b, a)), $"[{t.Name}] distance not symmetric {a}<->{b}.");
            if (a == b) Assert.That(dab, Is.EqualTo(0), $"[{t.Name}] Distance to self must be 0 at {a}.");
            else Assert.That(dab, Is.GreaterThan(0), $"[{t.Name}] distinct cells {a}, {b} must have positive distance.");
        }

        // 4. Line(a, b) is connected, starts at a, ends at b. "Connected" is graph-distance
        // <= 2 between consecutive cells, not strict adjacency: square's raycast line is
        // Bresenham, which steps diagonally through a shared corner (two cells that share a
        // 4-neighbor — distance 2) to preserve FOV behavior verbatim. Hex/triangle lines step
        // to true neighbors (distance 1), which this bound also permits. The point is to catch
        // a line that teleports (distance > 2), never to forbid the diagonal FOV step.
        private static void LineConnected(IGridTopology t, GridCoord a, GridCoord b)
        {
            var line = t.Line(a, b).ToList();
            Assert.That(line.First(), Is.EqualTo(a), $"[{t.Name}] Line({a},{b}) does not start at a.");
            Assert.That(line.Last(), Is.EqualTo(b), $"[{t.Name}] Line({a},{b}) does not end at b.");
            for (int i = 1; i < line.Count; i++)
            {
                Assert.That(line[i], Is.Not.EqualTo(line[i - 1]),
                    $"[{t.Name}] Line({a},{b}) repeats a cell {line[i]}.");
                Assert.That(t.Distance(line[i - 1], line[i]), Is.InRange(1, 2),
                    $"[{t.Name}] Line({a},{b}) has a disconnected jump {line[i - 1]}->{line[i]}.");
            }
        }

        // 3. Range(center, r) is exactly { x : Distance(center, x) <= r }.
        private static void RangeIsExactBall(IGridTopology t, GridCoord center, int radius)
        {
            var ball = t.Range(center, radius).ToHashSet();
            // Everything returned is within radius...
            foreach (var cell in ball)
                Assert.That(t.Distance(center, cell), Is.LessThanOrEqualTo(radius),
                    $"[{t.Name}] Range({center},{radius}) returned {cell} beyond the ball.");
            // ...and there are no duplicates.
            Assert.That(t.Range(center, radius).Count(), Is.EqualTo(ball.Count),
                $"[{t.Name}] Range({center},{radius}) returned duplicate cells.");
            // Completeness: every cell reachable within radius via BFS is present.
            var reachable = BfsBall(t, center, radius);
            Assert.That(ball.SetEquals(reachable),
                $"[{t.Name}] Range({center},{radius}) is not exactly the distance ball " +
                $"(missing {reachable.Except(ball).Count()}, extra {ball.Except(reachable).Count()}).");
        }

        // 6 is a static-analysis discipline (direction indices never persisted/wired); it has no
        // runtime assertion here — the golden/serialization tests cover that nothing wires an index.

        private static HashSet<GridCoord> BfsBall(IGridTopology t, GridCoord center, int radius)
        {
            var seen = new HashSet<GridCoord> { center };
            var frontier = new Queue<(GridCoord Cell, int Depth)>();
            frontier.Enqueue((center, 0));
            while (frontier.Count > 0)
            {
                var (cell, depth) = frontier.Dequeue();
                if (depth == radius) continue;
                foreach (var n in t.Neighbors(cell))
                    if (seen.Add(n))
                        frontier.Enqueue((n, depth + 1));
            }
            return seen;
        }

        private static IEnumerable<GridCoord> Sample(int extent, int z)
        {
            for (int y = -extent; y <= extent; y++)
            for (int x = -extent; x <= extent; x++)
                yield return new GridCoord(x, y, z);
        }

        private static int AngularDiff(int a, int b)
        {
            int d = ((a - b) % 360 + 360) % 360;
            return d > 180 ? 360 - d : d;
        }
    }

    [TestFixture]
    public class SquareTopologyInvariantTests
    {
        [Test]
        public void SquareTopology_SatisfiesAllInvariants()
            => GridTopologyInvariants.AssertAll(SquareTopology.Instance);
    }
}
