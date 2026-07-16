using System;
using System.Collections.Generic;

namespace Aetherium.Topology
{
    /// <summary>
    /// Triangular tiling (docs/grid-topologies.md P2) — the generalization proof: the first
    /// topology whose cells do NOT all share a direction set. A cell's orientation is its
    /// parity <c>(X+Y)&amp;1</c>: up-triangles (even) cross edges at {60,180,300}°, down-triangles
    /// (odd) at {0,120,240}°. Every edge crossing flips parity, and the returned heading is
    /// legal in the destination cell (the shared edge exists on both sides), so headings never
    /// desync. There are always exactly 3 edges — <see cref="MaxDirectionCount"/> is 3 and
    /// <see cref="HasUniformDirections"/> is false.
    ///
    /// Distance/Range are computed by breadth-first search (correct by construction — the
    /// triangular metric has no simple closed form because the ±X moves alternate between two
    /// axes by parity), and Line is a BFS-guided greedy descent that stays connected and hits
    /// both endpoints. These are the reference implementations; a closed-form metric is a
    /// future optimization behind this same interface. Triangle worlds are not performance-
    /// critical yet (melee/interaction resolve at distance 1, one BFS ring).
    /// </summary>
    public sealed class TriangleTopology : IPlanarGridTopology
    {
        public static TriangleTopology Instance { get; } = new TriangleTopology();

        private TriangleTopology() { }

        // Safety cap on BFS distance search — far beyond any perception/gameplay range on a
        // triangle world. A pair farther apart than this reports the cap (never used in the
        // metric-sensitive gameplay paths, which are all short-range).
        private const int MaxDistanceSearch = 512;

        private static readonly double Sqrt3Over2 = Math.Sqrt(3.0) / 2.0;
        private static readonly double Sqrt3Over6 = Math.Sqrt(3.0) / 6.0;

        // Ordered by ascending heading for determinism (indices are ephemeral either way).
        private static readonly (int Dx, int Dy, int Heading)[] UpSteps =
        {
            (+1, 0, 60),    // up-right edge
            (0, +1, 180),   // bottom (horizontal) edge → the down-cell below
            (-1, 0, 300),   // up-left edge
        };

        private static readonly (int Dx, int Dy, int Heading)[] DownSteps =
        {
            (0, -1, 0),     // top (horizontal) edge → the up-cell above
            (+1, 0, 120),   // down-right edge
            (-1, 0, 240),   // down-left edge
        };

        public string Name => "tri";
        public bool HasUniformDirections => false;
        public int MaxDirectionCount => 3;

        private static bool IsUp(GridCoord cell) => ((cell.X + cell.Y) & 1) == 0;

        private static (int Dx, int Dy, int Heading)[] StepsFor(GridCoord cell)
            => IsUp(cell) ? UpSteps : DownSteps;

        public int DirectionCount(GridCoord cell) => 3;

        public EdgeStep GetStep(GridCoord cell, int directionIndex)
        {
            var (dx, dy, heading) = StepsFor(cell)[directionIndex];
            return new EdgeStep(directionIndex, new GridCoord(cell.X + dx, cell.Y + dy, cell.Z), heading);
        }

        public IEnumerable<EdgeStep> Steps(GridCoord cell)
        {
            var table = StepsFor(cell);
            for (int i = 0; i < table.Length; i++)
            {
                var (dx, dy, heading) = table[i];
                yield return new EdgeStep(i, new GridCoord(cell.X + dx, cell.Y + dy, cell.Z), heading);
            }
        }

        public IEnumerable<GridCoord> Neighbors(GridCoord cell)
        {
            foreach (var (dx, dy, _) in StepsFor(cell))
                yield return new GridCoord(cell.X + dx, cell.Y + dy, cell.Z);
        }

        public int Distance(GridCoord a, GridCoord b)
        {
            if (a == b) return 0;
            var seen = new HashSet<GridCoord> { a };
            var frontier = new Queue<GridCoord>();
            frontier.Enqueue(a);
            int depth = 0;
            while (frontier.Count > 0 && depth < MaxDistanceSearch)
            {
                depth++;
                int layer = frontier.Count;
                for (int i = 0; i < layer; i++)
                {
                    var cell = frontier.Dequeue();
                    foreach (var n in Neighbors(cell))
                    {
                        if (n == b) return depth;
                        if (seen.Add(n)) frontier.Enqueue(n);
                    }
                }
            }
            return MaxDistanceSearch;
        }

        public IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            yield return a;
            if (a == b) yield break;

            var current = a;
            var (bx, by) = CellCenter(b);
            var (ax, ay) = CellCenter(a);
            // Guard against pathological loops with a length bound.
            int guard = MaxDistanceSearch * 2;
            while (current != b && guard-- > 0)
            {
                int currentDist = Distance(current, b);
                GridCoord? best = null;
                double bestPerp = double.MaxValue;

                foreach (var n in Neighbors(current))
                {
                    // Greedy descent toward b — only steps that shrink the metric.
                    if (Distance(n, b) != currentDist - 1) continue;
                    // Straightness tie-break: prefer the neighbor whose centroid is nearest the
                    // ideal a→b segment (perpendicular distance), so the line stays visually direct.
                    var (nx, ny) = CellCenter(n);
                    double perp = PerpDistance(ax, ay, bx, by, nx, ny);
                    if (perp < bestPerp)
                    {
                        bestPerp = perp;
                        best = n;
                    }
                }

                if (best is null) yield break; // unreachable (shouldn't happen on a full lattice)
                current = best.Value;
                yield return current;
            }
        }

        public IEnumerable<GridCoord> Range(GridCoord center, int radius)
        {
            var seen = new HashSet<GridCoord> { center };
            var frontier = new Queue<GridCoord>();
            frontier.Enqueue(center);
            yield return center;
            for (int depth = 0; depth < radius; depth++)
            {
                int layer = frontier.Count;
                for (int i = 0; i < layer; i++)
                {
                    var cell = frontier.Dequeue();
                    foreach (var n in Neighbors(cell))
                        if (seen.Add(n))
                        {
                            frontier.Enqueue(n);
                            yield return n;
                        }
                }
            }
        }

        public int SnapHeading(GridCoord cell, int degrees) => NearestEdge(cell, degrees).Heading;

        public int TurnStepDegrees(GridCoord cell) => 120; // cycles the cell's own three edges

        public int? HeadingToDirectionIndex(GridCoord cell, int degrees) => NearestEdge(cell, degrees).Index;

        public RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees, Aetherium.Model.RelativeDirection move)
            => AngularEdgeSelection.Resolve(this, cell, headingDegrees, move);

        public (double X, double Y) Delta(GridCoord from, GridCoord to)
        {
            var (fx, fy) = CellCenter(from);
            var (tx, ty) = CellCenter(to);
            return (tx - fx, ty - fy);
        }

        public (double X, double Y) CellCenter(GridCoord cell)
        {
            // Derived so the three edge directions land on the parity's headings exactly
            // (verified in TriangleTopologyTests): up centroids sit lower in their row, down
            // centroids higher, by ±sqrt(3)/6.
            int parity = (cell.X + cell.Y) & 1;
            double cx = 0.5 * cell.X;
            double cy = Sqrt3Over2 * cell.Y - Sqrt3Over6 * parity;
            return (cx, cy);
        }

        private (int Index, int Heading) NearestEdge(GridCoord cell, int degrees)
        {
            var table = StepsFor(cell);
            int bestIndex = 0;
            int bestDist = AngularEdgeSelection.AngularDistance(table[0].Heading, degrees);
            for (int i = 1; i < table.Length; i++)
            {
                int d = AngularEdgeSelection.AngularDistance(table[i].Heading, degrees);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }
            return (bestIndex, table[bestIndex].Heading);
        }

        private static double PerpDistance(double ax, double ay, double bx, double by, double px, double py)
        {
            double dx = bx - ax, dy = by - ay;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-12) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
            // |cross((p-a), (b-a))| / |b-a|
            double cross = Math.Abs((px - ax) * dy - (py - ay) * dx);
            return cross / Math.Sqrt(len2);
        }
    }
}
