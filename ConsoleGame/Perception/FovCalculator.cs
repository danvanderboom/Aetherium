using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Systems
{
    public class FovCalculator
    {
        public bool[,] ComputeVisible(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var visible = new bool[height, width];

            // Always see your own cell
            if (bounds.Contains(new Point(origin.X, origin.Y)))
                visible[origin.Y - bounds.Y, origin.X - bounds.X] = true;

            for (int by = bounds.Top; by < bounds.Bottom; by++)
            {
                for (int bx = bounds.Left; bx < bounds.Right; bx++)
                {
                    var target = new WorldLocation(bx, by, origin.Z);

                    // Skip origin (already visible)
                    if (target == origin)
                        continue;

                    // Clamp by range (Chebyshev is fine for square field; Euclidean also acceptable)
                    var dx = bx - origin.X;
                    var dy = by - origin.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > maxRange)
                        continue;

                    double cumulativeOpacity = 0.0;

                    foreach (var step in EnumerateLine(origin, target))
                    {
                        var stepPoint = new Point(step.X, step.Y);
                        
                        // Get opacity for this cell (even if outside bounds, it can still block)
                        var cellOpacity = GetCellOpacity(world, step);
                        var newCumulativeOpacity = cumulativeOpacity + cellOpacity;

                        // Check if this cell blocks vision BEFORE marking it visible
                        // If opacity reaches >= 1.0, this cell is the blocking cell and should be visible
                        // but nothing beyond it should be visible
                        if (newCumulativeOpacity > 1.0 - 1e-9)
                        {
                            // This cell blocks vision - mark it visible (you can see the blocking object)
                            // but don't mark anything beyond it
                            if (bounds.Contains(stepPoint))
                            {
                                visible[step.Y - bounds.Y, step.X - bounds.X] = true;
                            }
                            break; // fully blocked beyond this cell
                        }

                        // Cell doesn't block - mark it visible and continue
                        if (bounds.Contains(stepPoint))
                        {
                            visible[step.Y - bounds.Y, step.X - bounds.X] = true;
                        }

                        // Update cumulative opacity for next iteration
                        cumulativeOpacity = newCumulativeOpacity;
                    }
                }
            }

            return visible;
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
                    var ov = component as ObstructsView;
                    if (ov != null)
                        opacity += Clamp01(ov.Opacity);
                }
            }

            // Entities at this location (doors, objects, etc.)
            if (world.EntitiesByLocation.TryGetValue(location, out var entities))
            {
                foreach (var entity in entities.Values)
                {
                    if (entity.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp))
                    {
                        var opens = ocComp as OpensAndCloses;
                        if (opens != null && opens.IsOpen)
                            continue; // treat as transparent when open
                    }

                    if (entity.Components.TryGetValue(typeof(ObstructsView), out var ovComp))
                    {
                        var block = ovComp as ObstructsView;
                        if (block != null)
                            opacity += Clamp01(block.Opacity);
                    }
                }
            }

            // Cap to [0,1]
            return Math.Max(0.0, Math.Min(1.0, opacity));
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


