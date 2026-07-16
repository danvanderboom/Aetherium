using System;
using System.Collections.Generic;

namespace Aetherium.Topology
{
    /// <summary>
    /// Pointy-top hexagonal tiling (docs/hexagonal-tiles.md). Cells store axial
    /// coordinates (q, r) in <see cref="GridCoord"/> X/Y — no storage or wire change from
    /// square. Six uniform edges, cube-distance metric, cube-lerp lines, hex-disc ranges,
    /// facings at {30, 90, 150, 210, 270, 330}° (F/B along east–west are exact; every edge
    /// has an opposite). The planar embedding is scaled so adjacent centers are exactly one
    /// unit apart, keeping FOV/light range comparable to square.
    /// </summary>
    public sealed class HexTopology : IPlanarGridTopology
    {
        public static HexTopology Instance { get; } = new HexTopology();

        private HexTopology() { }

        // Axial neighbor steps, ordered by ascending compass heading (0 = north, clockwise;
        // engine axes +X east, +Y south). Headings are exact for this pointy-top embedding.
        private static readonly (int Dq, int Dr, int Heading)[] StepTable =
        {
            (+1, -1, 30),
            (+1, 0, 90),
            (0, +1, 150),
            (-1, +1, 210),
            (-1, 0, 270),
            (0, -1, 330),
        };

        private static readonly double Sqrt3Over2 = Math.Sqrt(3.0) / 2.0;

        public string Name => "hex";
        public bool HasUniformDirections => true;
        public int MaxDirectionCount => 6;

        public int DirectionCount(GridCoord cell) => 6;

        public EdgeStep GetStep(GridCoord cell, int directionIndex)
        {
            var (dq, dr, heading) = StepTable[directionIndex];
            return new EdgeStep(directionIndex, new GridCoord(cell.X + dq, cell.Y + dr, cell.Z), heading);
        }

        public IEnumerable<EdgeStep> Steps(GridCoord cell)
        {
            for (int i = 0; i < StepTable.Length; i++)
                yield return GetStep(cell, i);
        }

        public IEnumerable<GridCoord> Neighbors(GridCoord cell)
        {
            foreach (var (dq, dr, _) in StepTable)
                yield return new GridCoord(cell.X + dq, cell.Y + dr, cell.Z);
        }

        public int Distance(GridCoord a, GridCoord b)
        {
            // Cube distance: (|dx| + |dy| + |dz|) / 2 with cube x=q, z=r, y=-x-z.
            int dq = a.X - b.X;
            int dr = a.Y - b.Y;
            int ds = -dq - dr;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
        }

        public IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            int n = Distance(a, b);
            if (n == 0)
            {
                yield return a;
                yield break;
            }

            // Cube coordinates of both endpoints.
            double ax = a.X, az = a.Y, ay = -ax - az;
            double bx = b.X, bz = b.Y, by = -bx - bz;

            GridCoord prev = default;
            bool havePrev = false;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                var cell = CubeRound(
                    ax + (bx - ax) * t,
                    ay + (by - ay) * t,
                    az + (bz - az) * t,
                    a.Z);
                // Cube rounding can, on exact half-boundaries, repeat a cell; skip repeats so
                // consecutive Line cells are always distinct neighbors.
                if (havePrev && cell == prev)
                    continue;
                yield return cell;
                prev = cell;
                havePrev = true;
            }
        }

        public IEnumerable<GridCoord> Range(GridCoord center, int radius)
        {
            for (int dq = -radius; dq <= radius; dq++)
            {
                int rLo = Math.Max(-radius, -dq - radius);
                int rHi = Math.Min(radius, -dq + radius);
                for (int dr = rLo; dr <= rHi; dr++)
                    yield return new GridCoord(center.X + dq, center.Y + dr, center.Z);
            }
        }

        public int SnapHeading(GridCoord cell, int degrees)
        {
            int best = StepTable[0].Heading;
            int bestDist = AngularEdgeSelection.AngularDistance(best, degrees);
            for (int i = 1; i < StepTable.Length; i++)
            {
                int d = AngularEdgeSelection.AngularDistance(StepTable[i].Heading, degrees);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = StepTable[i].Heading;
                }
            }
            return best;
        }

        public int TurnStepDegrees(GridCoord cell) => 60;

        public int? HeadingToDirectionIndex(GridCoord cell, int degrees)
        {
            int best = 0;
            int bestDist = AngularEdgeSelection.AngularDistance(StepTable[0].Heading, degrees);
            for (int i = 1; i < StepTable.Length; i++)
            {
                int d = AngularEdgeSelection.AngularDistance(StepTable[i].Heading, degrees);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        public RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees, Aetherium.Model.RelativeDirection move)
            => AngularEdgeSelection.Resolve(this, cell, headingDegrees, move);

        public (double X, double Y) Delta(GridCoord from, GridCoord to)
        {
            var (fx, fy) = CellCenter(from);
            var (tx, ty) = CellCenter(to);
            return (tx - fx, ty - fy);
        }

        public (double X, double Y) CellCenter(GridCoord cell)
            // Normalized pointy-top embedding: adjacent centers are exactly 1 unit apart.
            => (cell.X + cell.Y / 2.0, Sqrt3Over2 * cell.Y);

        private static GridCoord CubeRound(double x, double y, double z, int level)
        {
            int rx = (int)Math.Round(x);
            int ry = (int)Math.Round(y);
            int rz = (int)Math.Round(z);

            double xDiff = Math.Abs(rx - x);
            double yDiff = Math.Abs(ry - y);
            double zDiff = Math.Abs(rz - z);

            if (xDiff > yDiff && xDiff > zDiff)
                rx = -ry - rz;
            else if (yDiff > zDiff)
                ry = -rx - rz;
            else
                rz = -rx - ry;

            // Back to axial: q = x, r = z.
            return new GridCoord(rx, rz, level);
        }
    }
}
