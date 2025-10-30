using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Lighting
{
    /// <summary>
    /// Core light propagation calculator using raycasting similar to FOV.
    /// Calculates light attenuation and shadow blocking based on opacity.
    /// </summary>
    public class LightCalculator
    {
        /// <summary>
        /// Computes light levels for all locations in the given bounds from a light source.
        /// Uses raycasting to propagate light, with attenuation by distance and shadow blocking by opacity.
        /// </summary>
        public void ComputeLightFromSource(
            World world,
            WorldLocation sourceLocation,
            double sourceIntensity,
            int maxRange,
            Rectangle bounds,
            LightFrame lightFrame)
        {
            if (sourceIntensity <= 0.0 || maxRange <= 0)
                return;

            // Light source location always gets full intensity
            if (bounds.Contains(new Point(sourceLocation.X, sourceLocation.Y)))
            {
                lightFrame.AddLightLevel(sourceLocation, sourceIntensity);
            }

            // Cast rays to all locations in bounds
            for (int by = bounds.Top; by < bounds.Bottom; by++)
            {
                for (int bx = bounds.Left; bx < bounds.Right; bx++)
                {
                    var target = new WorldLocation(bx, by, sourceLocation.Z);

                    // Skip source location (already handled)
                    if (target == sourceLocation)
                        continue;

                    // Check range
                    var dx = bx - sourceLocation.X;
                    var dy = by - sourceLocation.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    
                    if (distance > maxRange)
                        continue;

                    // Calculate light along ray
                    var lightReaching = CalculateLightAlongRay(
                        world, sourceLocation, target, sourceIntensity, distance, maxRange);

                    if (lightReaching > 0.0)
                    {
                        lightFrame.AddLightLevel(target, lightReaching);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates how much light reaches the target along a ray from source.
        /// Light is attenuated by distance and blocked by opacity of cells along the ray.
        /// </summary>
        private double CalculateLightAlongRay(
            World world,
            WorldLocation source,
            WorldLocation target,
            double sourceIntensity,
            double distance,
            int maxRange)
        {
            // Distance attenuation: linear falloff from sourceIntensity to 0 at maxRange
            var distanceAttenuation = sourceIntensity * (1.0 - (distance / maxRange));

            if (distanceAttenuation <= 0.0)
                return 0.0;

            // Calculate opacity blocking along the ray
            double cumulativeOpacity = 0.0;
            bool reachedTarget = false;

            foreach (var step in EnumerateLine(source, target))
            {
                // Accumulate opacity from terrain and entities
                cumulativeOpacity += GetCellOpacity(world, step);

                // If fully blocked, no light reaches target
                if (cumulativeOpacity >= 1.0 - 1e-9)
                    return 0.0;

                if (step == target)
                {
                    reachedTarget = true;
                    break;
                }
            }

            if (!reachedTarget)
                return 0.0;

            // Apply opacity blocking: remaining light = distanceAttenuation * (1 - cumulativeOpacity)
            var remainingLight = distanceAttenuation * (1.0 - cumulativeOpacity);
            return Math.Max(0.0, remainingLight);
        }

        /// <summary>
        /// Gets the opacity of a cell (from terrain and entities).
        /// Returns value 0.0-1.0 where 1.0 is fully opaque.
        /// Reuses logic from FovCalculator for consistency.
        /// </summary>
        private static double GetCellOpacity(World world, WorldLocation location)
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
                    // Open doors don't block light
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

        /// <summary>
        /// Enumerates cells along a line from start to end (Bresenham's algorithm).
        /// Includes the end cell, excludes the start cell.
        /// </summary>
        private static IEnumerable<WorldLocation> EnumerateLine(WorldLocation start, WorldLocation end)
        {
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

