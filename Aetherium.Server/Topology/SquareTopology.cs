using System;
using System.Collections.Generic;

namespace Aetherium.Topology
{
    /// <summary>
    /// Today's grid, behind the seam: 4 uniform cardinal edges, Manhattan metric,
    /// Bresenham lines (moved verbatim from the FOV/lighting calculators), diamond
    /// ranges, 90° facings. Every world without an explicit <c>topology</c> config
    /// runs on this singleton — byte-identically to the pre-seam engine (pinned by
    /// SquareTopologyGoldenTests).
    /// </summary>
    public sealed class SquareTopology : IPlanarGridTopology
    {
        public static SquareTopology Instance { get; } = new SquareTopology();

        private SquareTopology() { }

        // Clockwise from north. Engine convention: North = -Y, South = +Y (matches
        // World.TryMove and the perception stack). Direction indices are ephemeral —
        // never persisted or sent on the wire (invariant 6).
        private static readonly (int Dx, int Dy, int Heading)[] StepTable =
        {
            (0, -1, 0),    // North
            (+1, 0, 90),   // East
            (0, +1, 180),  // South
            (-1, 0, 270),  // West
        };

        public string Name => "square";
        public bool HasUniformDirections => true;
        public int MaxDirectionCount => 4;

        public int DirectionCount(GridCoord cell) => 4;

        public EdgeStep GetStep(GridCoord cell, int directionIndex)
        {
            var (dx, dy, heading) = StepTable[directionIndex];
            return new EdgeStep(directionIndex, new GridCoord(cell.X + dx, cell.Y + dy, cell.Z), heading);
        }

        public IEnumerable<EdgeStep> Steps(GridCoord cell)
        {
            for (int i = 0; i < StepTable.Length; i++)
                yield return GetStep(cell, i);
        }

        public IEnumerable<GridCoord> Neighbors(GridCoord cell)
        {
            foreach (var (dx, dy, _) in StepTable)
                yield return new GridCoord(cell.X + dx, cell.Y + dy, cell.Z);
        }

        public int Distance(GridCoord a, GridCoord b)
            => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>
        /// Bresenham's line on X/Y (verbatim from the former
        /// FovCalculator/LightCalculator EnumerateLine, which excluded the start cell —
        /// this contract includes both endpoints per invariant 4; raycast callers skip
        /// the origin themselves). Z is carried from <paramref name="a"/>.
        /// </summary>
        public IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            int x0 = a.X, y0 = a.Y;
            int x1 = b.X, y1 = b.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            int x = x0;
            int y = y0;

            while (true)
            {
                yield return new GridCoord(x, y, a.Z);

                if (x == x1 && y == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        public IEnumerable<GridCoord> Range(GridCoord center, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int span = radius - Math.Abs(dy);
                for (int dx = -span; dx <= span; dx++)
                    yield return new GridCoord(center.X + dx, center.Y + dy, center.Z);
            }
        }

        /// <summary>Nearest multiple of 90°, ties clockwise — the same boundaries as the
        /// legacy DegreesToCardinal (45° → East, 315° → North).</summary>
        public int SnapHeading(GridCoord cell, int degrees)
            => (AngularEdgeSelection.Normalize(degrees) + 45) / 90 % 4 * 90;

        public int TurnStepDegrees(GridCoord cell) => 90;

        public int? HeadingToDirectionIndex(GridCoord cell, int degrees)
            => (AngularEdgeSelection.Normalize(degrees) + 45) / 90 % 4;

        public RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees, Aetherium.Model.RelativeDirection move)
            => AngularEdgeSelection.Resolve(this, cell, headingDegrees, move);

        public (double X, double Y) Delta(GridCoord from, GridCoord to)
            => (to.X - from.X, to.Y - from.Y);

        public (double X, double Y) CellCenter(GridCoord cell)
            => (cell.X, cell.Y);
    }
}
