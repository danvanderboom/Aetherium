using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;

namespace Aetherium.Topology
{
    /// <summary>
    /// Uber's H3 hierarchical geospatial grid as an Aetherium topology (docs/h3-topology.md):
    /// a sphere tiled with hexagons plus exactly 12 pentagons per resolution, at any planetary,
    /// lunar, or planetoid scale. This is the "implementation, not a redesign" the seam was
    /// shaped for — the four things H3 adds each land on machinery already proven:
    /// <list type="bullet">
    /// <item>12 pentagons (5 neighbors) → per-cell <see cref="DirectionCount"/> (Rule 1), proven
    /// by triangle's parity sets and the PentagonishTopology CI mock.</item>
    /// <item>64-bit hierarchical index → packed losslessly into <see cref="GridCoord"/> X/Y
    /// (H3's reserved top bit keeps X non-negative).</item>
    /// <item>Spherical geometry (no global plane) → <see cref="Delta"/> as an azimuthal
    /// projection; this type deliberately does <b>not</b> implement <see cref="IPlanarGridTopology"/>.</item>
    /// <item>Nested resolutions → <see cref="IHierarchicalGridTopology"/>, traversed via the
    /// existing portal system, not the movement path.</item>
    /// </list>
    ///
    /// A <see cref="GridCoord"/> packs the H3 cell index: X = high 32 bits, Y = low 32 bits, and
    /// Z stays the vertical level (surface / sub-surface / orbital), orthogonal as always. Each
    /// index carries its own resolution, so the topology needs no resolution field and stays a
    /// stateless singleton like the others. Direction indices are ephemeral (invariant 6): a
    /// pentagon simply has five.
    /// </summary>
    public sealed class H3Topology : IHierarchicalGridTopology
    {
        public static H3Topology Instance { get; } = new H3Topology();

        private H3Topology() { }

        // Per-resolution mean edge length in great-circle radians (body-agnostic), memoized so
        // Delta stays cheap. Computed from a real cell→neighbor pair the first time a resolution
        // is seen, so it needs no Earth-specific constant.
        private static readonly ConcurrentDictionary<int, double> EdgeRadiansByResolution = new();

        public string Name => "h3";

        // Neighbor count varies (6 hexagons, 5 pentagons), so no uniform fast path.
        public bool HasUniformDirections => false;
        public int MaxDirectionCount => 6;

        // ---- packing ----

        private static H3Index ToIndex(GridCoord cell)
            => new H3Index(((ulong)(uint)cell.X << 32) | (uint)cell.Y);

        private static GridCoord ToCoord(H3Index index, int z)
        {
            ulong raw = index;
            return new GridCoord((int)(raw >> 32), (int)(raw & 0xFFFFFFFF), z);
        }

        /// <summary>Packs an H3 cell index into a <see cref="GridCoord"/> at vertical level
        /// <paramref name="z"/> — the boundary conversion worldgen uses when it samples the
        /// sphere by cell.</summary>
        public static GridCoord FromH3(ulong index, int z = 0) => ToCoord(new H3Index(index), z);

        /// <summary>The raw H3 cell index a coord packs — for worldgen / geospatial queries.</summary>
        public static ulong ToH3(GridCoord cell) => ToIndex(cell);

        // ---- per-cell direction machinery ----

        // Neighbors ordered deterministically by compass heading (0 = north, clockwise), so a
        // cell's direction indices are stable within a process. Indices are never persisted.
        private static List<(H3Index Neighbor, int Heading)> OrderedNeighbors(H3Index cell)
        {
            var result = new List<(H3Index, int)>(6);
            foreach (var ring in cell.GridDiskDistances(1))
            {
                if (ring.Distance != 1) continue; // skip the center cell (distance 0)
                result.Add((ring.Index, HeadingDegrees(cell, ring.Index)));
            }
            result.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return result;
        }

        public int DirectionCount(GridCoord cell)
            => ToIndex(cell).IsPentagon ? 5 : 6;

        public EdgeStep GetStep(GridCoord cell, int directionIndex)
        {
            var ordered = OrderedNeighbors(ToIndex(cell));
            var (neighbor, heading) = ordered[directionIndex];
            return new EdgeStep(directionIndex, ToCoord(neighbor, cell.Z), heading);
        }

        public IEnumerable<EdgeStep> Steps(GridCoord cell)
        {
            var ordered = OrderedNeighbors(ToIndex(cell));
            for (int i = 0; i < ordered.Count; i++)
                yield return new EdgeStep(i, ToCoord(ordered[i].Neighbor, cell.Z), ordered[i].Heading);
        }

        public IEnumerable<GridCoord> Neighbors(GridCoord cell)
        {
            int z = cell.Z;
            foreach (var (neighbor, _) in OrderedNeighbors(ToIndex(cell)))
                yield return ToCoord(neighbor, z);
        }

        // ---- metric & geometry (H3's own spherical operations) ----

        public int Distance(GridCoord a, GridCoord b)
            => ToIndex(a).GridDistance(ToIndex(b));

        public IEnumerable<GridCoord> Line(GridCoord a, GridCoord b)
        {
            int z = a.Z;
            foreach (var cell in ToIndex(a).GridPathCells(ToIndex(b)))
                yield return ToCoord(cell, z);
        }

        public IEnumerable<GridCoord> Range(GridCoord center, int radius)
        {
            int z = center.Z;
            foreach (var ring in ToIndex(center).GridDiskDistances(radius))
                yield return ToCoord(ring.Index, z);
        }

        // ---- heading machinery ----

        public int SnapHeading(GridCoord cell, int degrees)
        {
            var ordered = OrderedNeighbors(ToIndex(cell));
            int best = ordered[0].Heading;
            int bestDist = AngularEdgeSelection.AngularDistance(best, degrees);
            for (int i = 1; i < ordered.Count; i++)
            {
                int d = AngularEdgeSelection.AngularDistance(ordered[i].Heading, degrees);
                if (d < bestDist) { bestDist = d; best = ordered[i].Heading; }
            }
            return best;
        }

        // A cell's outgoing edges are evenly spread, so the rotate preset is 360/count
        // (60° for hexagons, 72° for pentagons).
        public int TurnStepDegrees(GridCoord cell) => 360 / DirectionCount(cell);

        public int? HeadingToDirectionIndex(GridCoord cell, int degrees)
        {
            var ordered = OrderedNeighbors(ToIndex(cell));
            if (ordered.Count == 0) return null;
            int best = 0;
            int bestDist = AngularEdgeSelection.AngularDistance(ordered[0].Heading, degrees);
            for (int i = 1; i < ordered.Count; i++)
            {
                int d = AngularEdgeSelection.AngularDistance(ordered[i].Heading, degrees);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        public RelativeMoveResolution ResolveRelative(GridCoord cell, int headingDegrees, Aetherium.Model.RelativeDirection move)
            => AngularEdgeSelection.Resolve(this, cell, headingDegrees, move);

        // ---- local planar embedding (azimuthal projection — no global plane) ----

        public (double X, double Y) Delta(GridCoord from, GridCoord to)
        {
            if (from == to) return (0.0, 0.0);

            var a = ToIndex(from);
            var b = ToIndex(to);
            var pa = a.ToLatLng();
            var pb = b.ToLatLng();

            // Great-circle displacement, projected onto the tangent plane at `from` and scaled to
            // cell-size units (adjacent centers ≈ 1 unit apart, like square/hex). Azimuth is the
            // geographic bearing (0 = north, clockwise), which is exactly the engine's heading
            // convention; the screen axes are +X east, +Y south (north = -Y).
            double azimuth = pa.GetAzimuthInRadians(pb);
            double gcDist = pa.GetGreatCircleDistanceInRadians(pb);
            double units = gcDist / EdgeRadians(a);
            return (units * Math.Sin(azimuth), -units * Math.Cos(azimuth));
        }

        // Heading from `cell` to an adjacent `neighbor`, in engine degrees (0 = north, clockwise).
        private static int HeadingDegrees(H3Index cell, H3Index neighbor)
        {
            double azimuth = cell.ToLatLng().GetAzimuthInRadians(neighbor.ToLatLng());
            int deg = (int)Math.Round(azimuth * 180.0 / Math.PI);
            return ((deg % 360) + 360) % 360;
        }

        private static double EdgeRadians(H3Index cell)
            => EdgeRadiansByResolution.GetOrAdd(cell.Resolution, _ =>
            {
                var center = cell.ToLatLng();
                double sum = 0; int n = 0;
                foreach (var ring in cell.GridDiskDistances(1))
                {
                    if (ring.Distance != 1) continue;
                    sum += center.GetGreatCircleDistanceInRadians(ring.Index.ToLatLng());
                    n++;
                }
                return n > 0 ? sum / n : 1.0;
            });

        // ---- hierarchy ----

        public int Resolution(GridCoord cell) => ToIndex(cell).Resolution;

        public GridCoord Parent(GridCoord cell)
        {
            var index = ToIndex(cell);
            return ToCoord(index.GetParentForResolution(index.Resolution - 1), cell.Z);
        }

        public IEnumerable<GridCoord> Children(GridCoord cell)
        {
            var index = ToIndex(cell);
            int z = cell.Z;
            foreach (var child in index.GetChildrenForResolution(index.Resolution + 1))
                yield return ToCoord(child, z);
        }
    }
}
