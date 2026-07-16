using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Topology;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// A deliberately irregular mock topology (docs/grid-topologies.md P3) — the permanent CI
    /// guard that no uniform-direction assumption regresses into the seam before H3 ever ships.
    /// It is hex with a symmetric matching of horizontal edges cut away: on y-even rows every
    /// cell loses one horizontal edge (degree 5 — a "pentagon"), on y-odd rows cells keep all
    /// six (degree 6). Because a cut removes the edge from BOTH endpoints, neighbor symmetry
    /// holds; BFS over the cut graph keeps Distance/Range a true metric/ball and greedy descent
    /// keeps Line connected. Geometry (Delta/CellCenter/headings) is borrowed from hex, so the
    /// kept edges stay heading↔Delta consistent. This mirrors exactly how H3's 12 pentagons —
    /// six-neighbor cells that happen to have five — will exercise the same machinery.
    /// </summary>
    public sealed class PentagonishTopology : IPlanarGridTopology
    {
        public static PentagonishTopology Instance { get; } = new PentagonishTopology();

        private const int MaxSearch = 256;
        private static readonly HexTopology Hex = HexTopology.Instance;

        public string Name => "pentagonish-mock";
        public bool HasUniformDirections => false;
        public int MaxDirectionCount => 6;

        // An edge is cut iff it is the east/west (heading 90/270) edge joining an even-x,even-y
        // cell with its east neighbor. Each such cell and its east neighbor lose exactly one
        // horizontal edge; the removal is symmetric because the edge is identified the same way
        // from either endpoint.
        private static bool IsHorizontalCutFromEast(GridCoord cell)
            => (cell.X & 1) == 0 && (cell.Y & 1) == 0;   // this cell loses its 90° (east) edge

        private static bool IsHorizontalCutFromWest(GridCoord cell)
            => (cell.X & 1) == 1 && (cell.Y & 1) == 0;   // this cell loses its 270° (west) edge

        public int DirectionCount(GridCoord cell) => Steps(cell).Count();

        public IEnumerable<EdgeStep> Steps(GridCoord cell)
        {
            int index = 0;
            foreach (var step in Hex.Steps(cell))
            {
                if (step.HeadingDegrees == 90 && IsHorizontalCutFromEast(cell)) continue;
                if (step.HeadingDegrees == 270 && IsHorizontalCutFromWest(cell)) continue;
                // Re-index densely so DirectionIndex stays 0..DirectionCount-1 (still ephemeral).
                yield return new EdgeStep(index++, step.Target, step.HeadingDegrees);
            }
        }

        public EdgeStep GetStep(GridCoord cell, int directionIndex) => Steps(cell).ElementAt(directionIndex);

        public IEnumerable<GridCoord> Neighbors(GridCoord cell) => Steps(cell).Select(s => s.Target);

        public int Distance(GridCoord a, GridCoord b)
        {
            if (a == b) return 0;
            var seen = new HashSet<GridCoord> { a };
            var frontier = new Queue<GridCoord>();
            frontier.Enqueue(a);
            int depth = 0;
            while (frontier.Count > 0 && depth < MaxSearch)
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
            return MaxSearch;
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
                        if (seen.Add(n)) { frontier.Enqueue(n); yield return n; }
                }
            }
        }

        public IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            yield return a;
            if (a == b) yield break;
            var current = a;
            int guard = MaxSearch * 2;
            while (current != b && guard-- > 0)
            {
                int currentDist = Distance(current, b);
                GridCoord? best = null;
                foreach (var n in Neighbors(current))
                    if (Distance(n, b) == currentDist - 1) { best = n; break; }
                if (best is null) yield break;
                current = best.Value;
                yield return current;
            }
        }

        public int SnapHeading(GridCoord cell, int degrees) => Nearest(cell, degrees).HeadingDegrees;
        public int TurnStepDegrees(GridCoord cell) => 60;
        public int? HeadingToDirectionIndex(GridCoord cell, int degrees)
            => DirectionCount(cell) == 0 ? (int?)null : Nearest(cell, degrees).DirectionIndex;

        public RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees, Aetherium.Model.RelativeDirection move)
        {
            // The mock is never wired into gameplay; the harness doesn't exercise this path.
            // A minimal forward-only resolution keeps the type a valid IGridTopology.
            var step = Nearest(cell, headingDegrees);
            return new RelativeMoveResolution(true, step, step.HeadingDegrees, null);
        }

        public (double X, double Y) Delta(GridCoord from, GridCoord to) => Hex.Delta(from, to);
        public (double X, double Y) CellCenter(GridCoord cell) => Hex.CellCenter(cell);

        private EdgeStep Nearest(GridCoord cell, int degrees)
        {
            EdgeStep best = default;
            int bestDist = int.MaxValue;
            foreach (var s in Steps(cell))
            {
                int d = ((s.HeadingDegrees - degrees) % 360 + 360) % 360;
                d = d > 180 ? 360 - d : d;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }
    }
}
