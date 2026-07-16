using System;
using NUnit.Framework;
using Aetherium.Topology;
using ModelRel = Aetherium.Model.RelativeDirection;

namespace Aetherium.Test.Topology
{
    /// <summary>
    /// Golden-master equivalence for the square seam (docs/grid-topologies.md P0 gate): the
    /// two helpers deleted from GameMapGrain (DegreesToCardinal, RotateRelativeByHeading) and
    /// the two direction tables deleted from World live on here as reference implementations,
    /// and every one is asserted byte-identical to the SquareTopology that replaced them,
    /// swept across all headings. If SquareTopology ever drifts from legacy square behavior,
    /// one of these fails.
    /// </summary>
    [TestFixture]
    public class SquareTopologyGoldenTests
    {
        private static readonly SquareTopology T = SquareTopology.Instance;

        // ---- reference implementations (verbatim from the pre-seam engine) ----

        private static Aetherium.WorldDirection LegacyDegreesToCardinal(int degrees)
        {
            int n = ((degrees % 360) + 360) % 360;
            if (n < 45 || n >= 315) return Aetherium.WorldDirection.North;
            if (n < 135) return Aetherium.WorldDirection.East;
            if (n < 225) return Aetherium.WorldDirection.South;
            return Aetherium.WorldDirection.West;
        }

        private static Aetherium.WorldDirection LegacyRotateRelativeByHeading(ModelRel rel, Aetherium.WorldDirection heading)
        {
            int rotations = rel switch
            {
                ModelRel.Forward => 0,
                ModelRel.Right => 1,
                ModelRel.Backward => 2,
                ModelRel.Left => 3,
                _ => 0,
            };
            var d = heading;
            for (int i = 0; i < rotations; i++)
                d = d switch
                {
                    Aetherium.WorldDirection.North => Aetherium.WorldDirection.East,
                    Aetherium.WorldDirection.East => Aetherium.WorldDirection.South,
                    Aetherium.WorldDirection.South => Aetherium.WorldDirection.West,
                    Aetherium.WorldDirection.West => Aetherium.WorldDirection.North,
                    _ => d,
                };
            return d;
        }

        private static (int Dx, int Dy) LegacyCardinalDelta(Aetherium.WorldDirection d) => d switch
        {
            Aetherium.WorldDirection.North => (0, -1),
            Aetherium.WorldDirection.South => (0, +1),
            Aetherium.WorldDirection.East => (+1, 0),
            Aetherium.WorldDirection.West => (-1, 0),
            _ => (0, 0),
        };

        // ---- the equivalence sweeps ----

        [Test]
        public void ResolveRelative_Matches_Legacy_CardinalizeThenRotate([Range(0, 359, 1)] int heading)
        {
            var cell = new GridCoord(10, 10, 0);
            foreach (ModelRel rel in new[] { ModelRel.Forward, ModelRel.Right, ModelRel.Backward, ModelRel.Left })
            {
                // Legacy: turn the heading into a cardinal, rotate it by the relative, step that cardinal.
                var legacyBearing = LegacyDegreesToCardinal(heading);
                var legacyDir = LegacyRotateRelativeByHeading(rel, legacyBearing);
                var (dx, dy) = LegacyCardinalDelta(legacyDir);
                var expected = new GridCoord(cell.X + dx, cell.Y + dy, cell.Z);

                var resolution = T.ResolveRelative(cell, heading, rel);

                Assert.That(resolution.Success, Is.True, $"heading {heading}, {rel}");
                Assert.That(resolution.Step.Target, Is.EqualTo(expected),
                    $"heading {heading}, {rel}: topology stepped {resolution.Step.Target}, legacy stepped {expected}.");
            }
        }

        [Test]
        public void SnapHeading_Matches_Legacy_DegreesToCardinal([Range(0, 359, 1)] int heading)
        {
            var cell = new GridCoord(0, 0, 0);
            var legacyCardinal = LegacyDegreesToCardinal(heading);
            int expectedDegrees = legacyCardinal switch
            {
                Aetherium.WorldDirection.North => 0,
                Aetherium.WorldDirection.East => 90,
                Aetherium.WorldDirection.South => 180,
                Aetherium.WorldDirection.West => 270,
                _ => 0,
            };
            Assert.That(T.SnapHeading(cell, heading), Is.EqualTo(expectedDegrees), $"heading {heading}");
        }

        [Test]
        public void HeadingToDirectionIndex_StepsToTheSnappedCardinalCell([Range(0, 359, 5)] int heading)
        {
            var cell = new GridCoord(3, 7, 0);
            var idx = T.HeadingToDirectionIndex(cell, heading);
            Assert.That(idx, Is.Not.Null);

            var (dx, dy) = LegacyCardinalDelta(LegacyDegreesToCardinal(heading));
            var expected = new GridCoord(cell.X + dx, cell.Y + dy, cell.Z);
            Assert.That(T.GetStep(cell, idx!.Value).Target, Is.EqualTo(expected), $"heading {heading}");
        }

        [Test]
        public void Distance_Is_Manhattan()
        {
            var a = new GridCoord(2, 3, 0);
            var b = new GridCoord(-4, 8, 0);
            Assert.That(T.Distance(a, b), Is.EqualTo(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)));
        }

        [Test]
        public void TurnStep_Is_90() => Assert.That(T.TurnStepDegrees(new GridCoord(0, 0, 0)), Is.EqualTo(90));

        [Test]
        public void Range_Is_The_Diamond_Ball()
        {
            var center = new GridCoord(0, 0, 0);
            const int r = 4;
            foreach (var cell in T.Range(center, r))
                Assert.That(T.Distance(center, cell), Is.LessThanOrEqualTo(r));
            // Count of a Manhattan ball of radius r is 2r^2 + 2r + 1.
            Assert.That(System.Linq.Enumerable.Count(T.Range(center, r)), Is.EqualTo(2 * r * r + 2 * r + 1));
        }

        [Test]
        public void Delta_Is_The_Raw_Coordinate_Difference()
        {
            var from = new GridCoord(5, 5, 0);
            var to = new GridCoord(9, 2, 0);
            var (dx, dy) = T.Delta(from, to);
            Assert.That(dx, Is.EqualTo(4));
            Assert.That(dy, Is.EqualTo(-3));
        }

        // The former FovCalculator/LightCalculator EnumerateLine (Bresenham, EXCLUDING the
        // start cell). SquareTopology.Line INCLUDES the start; both raycast callers skip the
        // origin themselves, so start-excluded reference vs. start-skipped topology must agree
        // on every cell after the origin — for arbitrary endpoints and octants.
        private static System.Collections.Generic.IEnumerable<GridCoord> LegacyEnumerateLine(GridCoord start, GridCoord end)
        {
            int x0 = start.X, y0 = start.Y, x1 = end.X, y1 = end.Y;
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int x = x0, y = y0;
            while (true)
            {
                if (!(x == x0 && y == y0))
                    yield return new GridCoord(x, y, start.Z);
                if (x == x1 && y == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        [Test]
        public void Line_Matches_Legacy_Bresenham_ExcludingOrigin(
            [Values(-6, -1, 0, 1, 5)] int ex, [Values(-4, 0, 3, 7)] int ey)
        {
            var origin = new GridCoord(0, 0, 0);
            var end = new GridCoord(ex, ey, 0);

            var legacy = new System.Collections.Generic.List<GridCoord>(LegacyEnumerateLine(origin, end));
            var topo = new System.Collections.Generic.List<GridCoord>();
            foreach (var c in T.Line(origin, end))
                if (c != origin) topo.Add(c); // raycast callers skip the origin the same way

            Assert.That(topo, Is.EqualTo(legacy), $"line to ({ex},{ey}) diverged from legacy Bresenham.");
        }
    }

    [TestFixture]
    public class GridTopologyRegistryTests
    {
        [Test]
        public void Omitted_Or_Empty_Resolves_To_Square()
        {
            Assert.That(GridTopologyRegistry.Get(null).Name, Is.EqualTo("square"));
            Assert.That(GridTopologyRegistry.Get("").Name, Is.EqualTo("square"));
            Assert.That(GridTopologyRegistry.Get("   ").Name, Is.EqualTo("square"));
        }

        [Test]
        public void Square_Is_Case_Insensitive()
            => Assert.That(GridTopologyRegistry.Get("Square").Name, Is.EqualTo("square"));

        [Test]
        public void Unknown_Name_Throws()
            => Assert.Throws<ArgumentException>(() => GridTopologyRegistry.Get("dodecahedron"));

        [Test]
        public void TryGet_Reports_Unknown_As_False()
        {
            Assert.That(GridTopologyRegistry.TryGet("hexagons-someday", out _), Is.False);
            Assert.That(GridTopologyRegistry.TryGet("square", out var sq), Is.True);
            Assert.That(sq.Name, Is.EqualTo("square"));
        }
    }
}
