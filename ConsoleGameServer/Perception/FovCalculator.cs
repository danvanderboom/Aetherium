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
        private bool useShadowCasting = true; // Enable for debugging

        public bool[,] ComputeVisible(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            if (useShadowCasting)
            {
                return ComputeVisibleShadowCasting(world, origin, bounds, maxRange);
            }
            else
            {
                return ComputeVisibleRayCasting(world, origin, bounds, maxRange);
            }
        }

        /// <summary>
        /// Efficient shadow casting algorithm - processes cells in columns outward from origin,
        /// tracking shadow regions to skip already-blocked areas. Much more efficient than
        /// casting rays to every cell individually.
        /// </summary>
        private bool[,] ComputeVisibleShadowCasting(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var visible = new bool[height, width];

            // Always see your own cell
            if (bounds.Contains(new Point(origin.X, origin.Y)))
                visible[origin.Y - bounds.Y, origin.X - bounds.X] = true;

            // Process all 8 octants (directions)
            for (int octant = 0; octant < 8; octant++)
            {
                CastShadowOctant(world, origin, bounds, maxRange, visible, octant);
            }

            return visible;
        }

        /// <summary>
        /// Original ray casting algorithm - casts a ray to every cell. Kept for comparison/fallback.
        /// </summary>
        private bool[,] ComputeVisibleRayCasting(World world, WorldLocation origin, Rectangle bounds, int maxRange)
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

        /// <summary>
        /// Casts shadow for a single octant (1/8 of the circle around origin).
        /// Processes columns outward, tracking shadow regions to skip blocked areas.
        /// </summary>
        private void CastShadowOctant(World world, WorldLocation origin, Rectangle bounds, int maxRange,
            bool[,] visible, int octant)
        {
            // Shadow regions are tracked as slopes (start/end of blocked angles)
            var shadowRegions = new List<(double start, double end)>();
            
            // Process columns outward from origin (distance 1 to maxRange)
            for (int distance = 1; distance <= maxRange; distance++)
            {
                // Determine which cells are in this column for this octant
                var columnCells = GetOctantColumnCells(origin, bounds, octant, distance);
                
                if (columnCells.Count == 0)
                    continue; // No cells in this column within bounds

                // Process each cell in the column
                foreach (var (x, y) in columnCells)
                {
                    var location = new WorldLocation(x, y, origin.Z);
                    
                    // Check Euclidean distance - skip if beyond maxRange
                    var dx = x - origin.X;
                    var dy = y - origin.Y;
                    var euclideanDistance = Math.Sqrt(dx * dx + dy * dy);
                    if (euclideanDistance > maxRange)
                        continue;
                    
                    // Calculate slope angles for this cell's edges
                    var cellStartSlope = GetSlope(dx - 0.5, dy - 0.5);
                    var cellEndSlope = GetSlope(dx + 0.5, dy + 0.5);
                    
                    // Check if this cell is in shadow
                    bool inShadow = false;
                    foreach (var (shadowStart, shadowEnd) in shadowRegions)
                    {
                        if (cellEndSlope <= shadowStart || cellStartSlope >= shadowEnd)
                            continue; // No overlap
                        inShadow = true;
                        break;
                    }

                    if (inShadow)
                    {
                        // Skip this cell - it's fully in shadow
                        continue;
                    }

                    // Cast a ray to this cell to check visibility and accumulate opacity
                    double cumulativeOpacity = 0.0;
                    bool blocksVision = false;
                    WorldLocation? blockingCell = null;

                    foreach (var step in EnumerateLine(origin, location))
                    {
                        // Get opacity for this step
                        var cellOpacity = GetCellOpacity(world, step);
                        var newCumulativeOpacity = cumulativeOpacity + cellOpacity;

                        // Check if this step blocks vision
                        if (newCumulativeOpacity > 1.0 - 1e-9)
                        {
                            // Mark the blocking cell as visible
                            var stepPoint = new Point(step.X, step.Y);
                            if (bounds.Contains(stepPoint))
                            {
                                visible[step.Y - bounds.Y, step.X - bounds.X] = true;
                            }
                            blockingCell = step;
                            blocksVision = true;
                            break;
                        }

                        // Mark this step as visible if in bounds
                        var stepPt = new Point(step.X, step.Y);
                        if (bounds.Contains(stepPt))
                        {
                            visible[step.Y - bounds.Y, step.X - bounds.X] = true;
                        }

                        cumulativeOpacity = newCumulativeOpacity;
                    }

                    // If the ray was blocked, add shadow region based on the blocking cell's angle range
                    // This ensures all cells in this angle range beyond the blocking point are also shadowed
                    if (blocksVision && blockingCell != null)
                    {
                        // Calculate the blocking cell's angle range relative to origin
                        var blockDx = blockingCell.X - origin.X;
                        var blockDy = blockingCell.Y - origin.Y;
                        var blockStartSlope = GetSlope(blockDx - 0.5, blockDy - 0.5);
                        var blockEndSlope = GetSlope(blockDx + 0.5, blockDy + 0.5);
                        
                        // Use the blocking cell's angle range for the shadow region
                        // This is more accurate than using the target cell's angle range
                        AddShadowRegion(shadowRegions, blockStartSlope, blockEndSlope);
                    }
                    else
                    {
                        // Ray reached the target cell - mark it as visible
                        if (bounds.Contains(new Point(x, y)))
                        {
                            visible[y - bounds.Y, x - bounds.X] = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all cells in a column at a specific distance for a given octant.
        /// </summary>
        private List<(int x, int y)> GetOctantColumnCells(WorldLocation origin, Rectangle bounds, int octant, int distance)
        {
            var cells = new List<(int x, int y)>();
            
            // Transform coordinates based on octant
            // Octant 0: East,  1: Northeast,  2: North,  3: Northwest
            // Octant 4: West, 5: Southwest,  6: South,  7: Southeast
            for (int i = 0; i <= distance; i++)
            {
                int x, y;
                
                switch (octant)
                {
                    case 0: // East
                        x = origin.X + distance;
                        y = origin.Y + i;
                        break;
                    case 1: // Northeast
                        x = origin.X + i;
                        y = origin.Y + distance;
                        break;
                    case 2: // North
                        x = origin.X - i;
                        y = origin.Y + distance;
                        break;
                    case 3: // Northwest
                        x = origin.X - distance;
                        y = origin.Y + i;
                        break;
                    case 4: // West
                        x = origin.X - distance;
                        y = origin.Y - i;
                        break;
                    case 5: // Southwest
                        x = origin.X - i;
                        y = origin.Y - distance;
                        break;
                    case 6: // South
                        x = origin.X + i;
                        y = origin.Y - distance;
                        break;
                    case 7: // Southeast
                        x = origin.X + distance;
                        y = origin.Y - i;
                        break;
                    default:
                        continue;
                }

                if (bounds.Contains(new Point(x, y)))
                {
                    cells.Add((x, y));
                }
            }
            
            return cells;
        }

        /// <summary>
        /// Calculates slope from (0,0) to (dx, dy). Used for shadow region tracking.
        /// </summary>
        private double GetSlope(double dx, double dy)
        {
            if (dx == 0)
                return double.PositiveInfinity; // Vertical line
            return dy / dx;
        }

        /// <summary>
        /// Adds a shadow region, merging with overlapping regions.
        /// </summary>
        private void AddShadowRegion(List<(double start, double end)> shadowRegions, double start, double end)
        {
            // Find overlapping regions and merge
            for (int i = shadowRegions.Count - 1; i >= 0; i--)
            {
                var (regionStart, regionEnd) = shadowRegions[i];
                
                // Check if we overlap or are adjacent
                if (end >= regionStart && start <= regionEnd)
                {
                    // Merge: extend the region
                    start = Math.Min(start, regionStart);
                    end = Math.Max(end, regionEnd);
                    shadowRegions.RemoveAt(i);
                }
            }
            
            // Find insertion point to keep list sorted
            int insertIndex = 0;
            for (int i = 0; i < shadowRegions.Count; i++)
            {
                if (start <= shadowRegions[i].start)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
            
            shadowRegions.Insert(insertIndex, (start, end));
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


