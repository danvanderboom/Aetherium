using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Lighting
{
    /// <summary>
    /// Calculates sunlight position and illumination with directional shadows.
    /// Supports day/night cycle with sunrise/sunset color effects.
    /// </summary>
    public class SunlightCalculator
    {
        /// <summary>
        /// Calculates the sun's position in the sky based on time of day.
        /// </summary>
        /// <param name="timeOfDay">Hour of day (0-24)</param>
        /// <returns>(azimuth in degrees, elevation in degrees)</returns>
        public (double azimuth, double elevation) CalculateSunPosition(double timeOfDay)
        {
            // Normalize to 0-24 range
            timeOfDay = timeOfDay % 24.0;
            if (timeOfDay < 0) timeOfDay += 24.0;

            // Azimuth: 0° at midnight (north), 90° at 6am (east), 180° at noon (south), 270° at 6pm (west)
            double azimuth = (timeOfDay / 24.0) * 360.0;

            // Elevation: -90° at midnight, +90° at noon
            // Use sine wave: sin(0) = 0 at sunrise(6am), sin(π/2) = 1 at noon, sin(π) = 0 at sunset(6pm)
            // Shift by -π/2 so: sin(-π/2) = -1 at midnight
            double hourAngle = (timeOfDay / 24.0) * 2 * Math.PI - Math.PI / 2; // Maps 0hr→-π/2, 6hr→0, 12hr→π/2, 18hr→π
            double elevation = Math.Sin(hourAngle) * 90.0;

            return (azimuth, elevation);
        }

        /// <summary>
        /// Gets the sunlight color and intensity based on sun elevation.
        /// Low elevation (sunrise/sunset) produces reddish tint.
        /// </summary>
        /// <param name="elevation">Sun elevation in degrees</param>
        /// <returns>(red, green, blue, intensity) all 0.0-1.0</returns>
        public (double r, double g, double b, double intensity) GetSunlightColor(double elevation)
        {
            // Below horizon: no sunlight
            if (elevation < -15.0)
                return (0.0, 0.0, 0.0, 0.0);

            // Twilight zone (-15° to 0°): very dim light
            if (elevation < 0.0)
            {
                double twilightFactor = (elevation + 15.0) / 15.0; // 0-1
                double intensity = 0.2 * twilightFactor;
                return (1.0, 0.7, 0.5, intensity); // Dim reddish-orange
            }

            // Sunrise/sunset zone (0° to 15°): reddish-orange tint
            if (elevation < 15.0)
            {
                double sunriseFactor = elevation / 15.0; // 0-1
                double intensity = 0.2 + (0.5 * sunriseFactor); // 0.2-0.7
                double r = 1.0;
                double g = 0.6 + (0.3 * sunriseFactor); // 0.6-0.9
                double b = 0.4 + (0.4 * sunriseFactor); // 0.4-0.8
                return (r, g, b, intensity);
            }

            // Normal daylight (15° to 90°): white light, full intensity
            double dayIntensity = 0.7 + (0.3 * Math.Min(elevation / 45.0, 1.0)); // 0.7-1.0
            return (1.0, 1.0, 1.0, dayIntensity);
        }

        /// <summary>
        /// Computes sunlight illumination for a bounded area using directional lighting.
        /// Uses reverse ray tracing from each cell toward the sun.
        /// </summary>
        public void ComputeSunlight(
            World world,
            Rectangle bounds,
            int zLevel,
            double timeOfDay,
            LightFrame lightFrame)
        {
            var (azimuth, elevation) = CalculateSunPosition(timeOfDay);
            var (r, g, b, intensity) = GetSunlightColor(elevation);

            // If no light, skip computation
            if (intensity <= 0.0)
                return;

            // Convert azimuth and elevation to 3D direction vector pointing TOWARD sun
            double azimuthRad = azimuth * Math.PI / 180.0;
            double elevationRad = elevation * Math.PI / 180.0;

            // Direction TO sun (we'll trace rays backwards from cells to sun)
            double dx = Math.Cos(elevationRad) * Math.Sin(azimuthRad);
            double dy = Math.Cos(elevationRad) * Math.Cos(azimuthRad);
            double dz = Math.Sin(elevationRad);

            Vector3 sunDirection = new Vector3((float)dx, (float)dy, (float)dz);
            sunDirection = Vector3.Normalize(sunDirection);

            // For each cell in bounds, trace a ray toward sun to check for blocking
            for (int by = bounds.Top; by < bounds.Bottom; by++)
            {
                for (int bx = bounds.Left; bx < bounds.Right; bx++)
                {
                    var location = new WorldLocation(bx, by, zLevel);
                    
                    // Check if this cell is blocked from sunlight
                    double lightLevel = TraceSunlightRay(world, location, sunDirection, intensity);
                    
                    if (lightLevel > 0.0)
                    {
                        lightFrame.AddLightLevel(location, lightLevel);
                    }
                }
            }
        }

        /// <summary>
        /// Traces a ray from a location toward the sun to determine light reaching that location.
        /// Uses the sun direction vector to cast shadows properly.
        /// </summary>
        private double TraceSunlightRay(World world, WorldLocation origin, Vector3 sunDirection, double baseIntensity)
        {
            // Check if origin cell itself is opaque (inside a wall)
            double originOpacity = GetCellOpacity(world, origin);
            if (originOpacity >= 0.9)
                return 0.0; // Inside solid geometry, no light

            // Trace ray toward sun for a reasonable distance
            const int maxSteps = 50;
            const double stepSize = 0.5;

            Vector3 pos = new Vector3(origin.X, origin.Y, origin.Z);
            double cumulativeOpacity = 0.0;

            for (int step = 1; step <= maxSteps; step++)
            {
                // Move toward sun (scalar multiplication)
                pos = new Vector3(
                    pos.X + sunDirection.X * (float)stepSize,
                    pos.Y + sunDirection.Y * (float)stepSize,
                    pos.Z + sunDirection.Z * (float)stepSize
                );

                // Check opacity at this position
                var checkLoc = new WorldLocation((int)Math.Round(pos.X), (int)Math.Round(pos.Y), (int)Math.Round(pos.Z));
                double opacity = GetCellOpacity(world, checkLoc);

                cumulativeOpacity += opacity * stepSize; // Accumulate opacity

                // If fully blocked, no light reaches origin
                if (cumulativeOpacity >= 0.99)
                    return 0.0;

                // If we've gone far enough (escaped local shadows), assume sunlit
                if (step >= maxSteps)
                    break;
            }

            // Apply accumulated opacity to reduce light
            double lightReaching = baseIntensity * (1.0 - Math.Min(cumulativeOpacity, 1.0));
            return Math.Max(0.0, lightReaching);
        }

        /// <summary>
        /// Gets the opacity of a cell (from terrain and entities).
        /// Returns value 0.0-1.0 where 1.0 is fully opaque (blocks all light).
        /// </summary>
        private static double GetCellOpacity(World world, WorldLocation location)
        {
            // Check terrain opacity
            var terrain = world.GetTerrain(location);
            if (terrain != null)
            {
                var tileType = world.GetTerrainType(location);
                if (tileType != null)
                {
                    // Walls are opaque, floors are transparent
                    if (tileType.Settings.TryGetValue("OpacityOverride", out var opacityStr))
                    {
                        if (double.TryParse(opacityStr, out var opacity))
                            return opacity;
                    }

                    // Default: walls block light, floors don't
                    bool blocksLight = tileType.Settings.TryGetValue("BlocksLight", out var blocks) && blocks == "true";
                    if (blocksLight)
                        return 1.0;
                }
            }

            // Check for entities that block light (doors, etc.)
            if (world.EntitiesByLocation.TryGetValue(location, out var entities))
            {
                foreach (var entity in entities.Values)
                {
                    // Check if entity blocks passage (doors, walls, etc.)
                    var door = entity.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                    if (door != null && !door.IsOpen)
                    {
                        // Closed doors block light
                        return 0.8; // Partial blocking
                    }
                }
            }

            return 0.0; // Transparent
        }
    }
}


