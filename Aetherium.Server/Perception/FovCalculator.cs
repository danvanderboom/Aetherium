using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Systems
{
    /// <summary>
    /// Per-cell ray-cast field of view. Casts a Bresenham line from origin to each cell
    /// inside the bounds, accumulating opacity along the way and stopping when the
    /// running sum reaches 1.0. The blocking cell itself is marked visible, but nothing
    /// beyond it is.
    ///
    /// This is O(width * height * average-ray-length). At typical viewport sizes
    /// (~40x20) that's well under a millisecond per perception update, so the extra
    /// complexity of a true shadow caster isn't justified — especially since shadow
    /// casting doesn't naturally support cumulative partial opacity (forest + forest
    /// + forest blocks; one forest does not).
    /// </summary>
    public class FovCalculator
    {
        public bool[,] ComputeVisible(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var visible = new bool[height, width];

            // Per-frame cell-opacity memo. Rays overlap heavily near the origin — a cell close to
            // the observer lies on almost every ray — so the naive cast recomputes each cell's
            // opacity ~5–6x per frame, and each recompute is two dict lookups + a LINQ scan of the
            // cell's entities (GetTerrain). Every line between two in-bounds points stays inside the
            // bounding box, so a plain (bounds-sized) array indexed by local (y,x) covers every step;
            // `opacityKnown` distinguishes "0.0, computed" from "not yet computed". Cuts terrain
            // lookups to one per unique cell. (perception efficiency)
            var opacityCache = new double[height, width];
            var opacityKnown = new bool[height, width];

            // Origin is always visible to itself when inside the bounds.
            if (bounds.Contains(new Point(origin.X, origin.Y)))
                visible[origin.Y - bounds.Y, origin.X - bounds.X] = true;

            for (int by = bounds.Top; by < bounds.Bottom; by++)
            {
                for (int bx = bounds.Left; bx < bounds.Right; bx++)
                {
                    var target = new WorldLocation(bx, by, origin.Z);
                    if (target == origin)
                        continue;

                    // Euclidean falloff in the topology's local embedding (Delta) —
                    // on square, exactly the raw coordinate difference used before.
                    var (dx, dy) = world.Topology.Delta(
                        Aetherium.Topology.GridCoord.From(origin),
                        Aetherium.Topology.GridCoord.From(target));
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > maxRange)
                        continue;

                    CastRay(world, origin, target, bounds, visible, opacityCache, opacityKnown);
                }
            }

            return visible;
        }

        /// <summary>Cell opacity via the per-frame memo when the cell is inside <paramref name="bounds"/>
        /// (the common case — every ray step is), else a direct uncached compute.</summary>
        private static double CachedOpacity(
            World world, WorldLocation cell, Rectangle bounds, double[,] cache, bool[,] known)
        {
            int lx = cell.X - bounds.X;
            int ly = cell.Y - bounds.Y;
            if (lx < 0 || ly < 0 || lx >= bounds.Width || ly >= bounds.Height)
                return GetCellOpacity(world, cell);
            if (!known[ly, lx])
            {
                cache[ly, lx] = GetCellOpacity(world, cell);
                known[ly, lx] = true;
            }
            return cache[ly, lx];
        }

        /// <summary>
        /// Walks the topology's line from origin to target (square: Bresenham), marking
        /// each cell visible until cumulative opacity reaches 1.0 (the blocking cell is
        /// the last marked). The origin cell is skipped — you see out of your own cell.
        /// </summary>
        private static void CastRay(World world, WorldLocation origin, WorldLocation target, Rectangle bounds,
            bool[,] visible, double[,] opacityCache, bool[,] opacityKnown)
        {
            double cumulativeOpacity = 0.0;
            var originCell = Aetherium.Topology.GridCoord.From(origin);

            foreach (var cell in world.Topology.Line(originCell, Aetherium.Topology.GridCoord.From(target)))
            {
                if (cell == originCell)
                    continue;

                var step = cell.ToWorldLocation();
                var stepPoint = new Point(step.X, step.Y);
                var cellOpacity = CachedOpacity(world, step, bounds, opacityCache, opacityKnown);
                cumulativeOpacity += cellOpacity;

                // Mark the cell visible whether or not it's the blocker — you can always
                // see the thing that's blocking your view.
                if (bounds.Contains(stepPoint))
                    visible[step.Y - bounds.Y, step.X - bounds.X] = true;

                // Use an epsilon so accumulated rounding (e.g. 0.49 + 0.49 = 0.98) doesn't
                // accidentally trip the blocker check.
                if (cumulativeOpacity > 1.0 - 1e-9)
                    return;
            }
        }

        /// <summary>An intervening band at or above this opacity blocks the vertical line of sight.</summary>
        public const double OpaqueThreshold = 1.0 - 1e-9;

        /// <summary>
        /// Sight opacity of band <paramref name="z"/> in column (x,y) for the vertical (up/down) line-of-sight test.
        /// Combines the opacity of things exactly at that cell (terrain tile + entities, via
        /// <see cref="GetCellOpacity"/>) with any Height-spanning <see cref="ObstructsView"/> obstruction that
        /// covers the band (via <see cref="World.ColumnViewOpacity"/>). A glass skylight (Opacity 0) contributes
        /// nothing even though it blocks movement, so a viewer sees through it.
        /// </summary>
        public static double BandVerticalOpacity(World world, int x, int y, int z)
        {
            var cell = new WorldLocation(x, y, z);
            var exact = GetCellOpacity(world, cell);
            var spanning = world.ColumnViewOpacity(x, y, z);
            return Math.Max(exact, spanning);
        }

        /// <summary>
        /// The bands (absolute Z, excluding the origin band) that are vertically visible in column (x,y): marching
        /// up and down from <paramref name="originZ"/> within the slab, each band is visible up to and including the
        /// first opaque band, which stops the ray (its surface — e.g. a bridge underside — is seen, nothing beyond).
        /// Depth is bounded by the slab range clamped to the world band range.
        /// </summary>
        public List<int> VerticalVisibleBands(World world, int x, int y, int originZ, int depthBelow, int depthAbove)
        {
            var bands = new List<int>();
            if (depthBelow <= 0 && depthAbove <= 0)
                return bands;

            int ceil = Math.Min(world.MaxBand, originZ + Math.Max(0, depthAbove));
            for (int z = originZ + 1; z <= ceil; z++)
            {
                bands.Add(z);
                if (BandVerticalOpacity(world, x, y, z) >= OpaqueThreshold)
                    break; // opaque band: its surface is visible, nothing above it
            }

            int floor = Math.Max(world.MinBand, originZ - Math.Max(0, depthBelow));
            for (int z = originZ - 1; z >= floor; z--)
            {
                bands.Add(z);
                if (BandVerticalOpacity(world, x, y, z) >= OpaqueThreshold)
                    break; // opaque band: its surface is visible, nothing below it
            }

            return bands;
        }

        public static double GetCellOpacity(World world, WorldLocation location)
        {
            double opacity = 0.0;

            // Terrain via TileType default components
            var terrainType = world.GetTerrainType(location);
            var tileType = terrainType?.TileType;
            if (tileType != null)
            {
                foreach (var component in tileType.DefaultComponents)
                {
                    if (component is ObstructsView ov)
                        opacity += Clamp01(ov.Opacity);
                }
            }

            // Entities at this location (doors, objects, etc.)
            if (world.EntitiesByLocation.TryGetValue(location, out var entities))
            {
                foreach (var entity in entities.Values)
                {
                    if (entity.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp)
                        && ocComp is OpensAndCloses opens
                        && opens.IsOpen)
                    {
                        continue; // open doors don't block
                    }

                    if (entity.Components.TryGetValue(typeof(ObstructsView), out var ovComp)
                        && ovComp is ObstructsView block)
                    {
                        opacity += Clamp01(block.Opacity);
                    }
                }
            }

            return Clamp01(opacity);
        }

        private static double Clamp01(double value) => value < 0 ? 0 : (value > 1 ? 1 : value);
    }
}
