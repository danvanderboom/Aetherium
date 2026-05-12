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

                    var dx = bx - origin.X;
                    var dy = by - origin.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > maxRange)
                        continue;

                    CastRay(world, origin, target, bounds, visible);
                }
            }

            return visible;
        }

        /// <summary>
        /// Walks a Bresenham line from origin to target, marking each cell visible until
        /// cumulative opacity reaches 1.0 (the blocking cell is the last marked).
        /// </summary>
        private static void CastRay(World world, WorldLocation origin, WorldLocation target, Rectangle bounds, bool[,] visible)
        {
            double cumulativeOpacity = 0.0;

            foreach (var step in EnumerateLine(origin, target))
            {
                var stepPoint = new Point(step.X, step.Y);
                var cellOpacity = GetCellOpacity(world, step);
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

        private static IEnumerable<WorldLocation> EnumerateLine(WorldLocation start, WorldLocation end)
        {
            // Bresenham's line algorithm (2D on X/Y), includes the end cell, excludes the start cell
            int x0 = start.X;
            int y0 = start.Y;
            int x1 = end.X;
            int y1 = end.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            int x = x0;
            int y = y0;

            while (true)
            {
                if (!(x == x0 && y == y0))
                    yield return new WorldLocation(x, y, start.Z);

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

        private static double Clamp01(double value) => value < 0 ? 0 : (value > 1 ? 1 : value);
    }
}
