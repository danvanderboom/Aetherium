using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Lighting
{
    /// <summary>
    /// Main lighting system that computes light levels for a view area.
    /// Aggregates light from all light sources in the world.
    /// </summary>
    public class LightingSystem
    {
        private readonly LightCalculator lightCalculator = new LightCalculator();

        /// <summary>
        /// Computes a LightFrame for the specified bounds by aggregating light from all enabled light sources.
        /// </summary>
        public LightFrame ComputeLighting(World world, Rectangle bounds, int zLevel)
        {
            var lightFrame = new LightFrame();

            // Find all entities with LightSource components that are enabled
            var lightSources = FindLightSources(world, zLevel);

            // Compute light from each source
            foreach (var (entity, lightSource, location) in lightSources)
            {
                if (!lightSource.IsEnabled)
                    continue;

                // Only process sources whose range might affect the bounds
                if (IsSourceInRange(location, bounds, lightSource.Range))
                {
                    lightCalculator.ComputeLightFromSource(
                        world,
                        location,
                        lightSource.Intensity,
                        lightSource.Range,
                        bounds,
                        lightFrame);
                }
            }

            // Clamp all light levels to [0.0, 1.0] (in case multiple sources exceeded 1.0)
            ClampLightLevels(lightFrame);

            return lightFrame;
        }

        /// <summary>
        /// Finds all entities with LightSource components at the specified Z level.
        /// Returns tuples of (Entity, LightSource, WorldLocation).
        /// </summary>
        private IEnumerable<(Entity entity, LightSource lightSource, WorldLocation location)> FindLightSources(
            World world, int zLevel)
        {
            foreach (var entity in world.Entities.Values)
            {
                // Check if entity has WorldLocation component before trying to get it
                if (!entity.Has<WorldLocation>())
                    continue;

                var location = entity.Get<WorldLocation>();
                if (location == null || location.Z != zLevel)
                    continue;

                // Check if entity has LightSource component before trying to get it
                if (!entity.Has<LightSource>())
                    continue;

                var lightSource = entity.Get<LightSource>();
                if (lightSource != null)
                {
                    yield return (entity, lightSource, location);
                }
            }
        }

        /// <summary>
        /// Checks if a light source location is within range to affect the bounds.
        /// </summary>
        private bool IsSourceInRange(WorldLocation sourceLocation, Rectangle bounds, int sourceRange)
        {
            // Calculate distance from source to nearest and farthest points of bounds
            var boundsCenterX = bounds.X + bounds.Width / 2.0;
            var boundsCenterY = bounds.Y + bounds.Height / 2.0;
            
            var maxDistanceFromSource = Math.Sqrt(
                Math.Pow(Math.Max(
                    Math.Abs(bounds.Left - sourceLocation.X),
                    Math.Abs(bounds.Right - 1 - sourceLocation.X)), 2) +
                Math.Pow(Math.Max(
                    Math.Abs(bounds.Top - sourceLocation.Y),
                    Math.Abs(bounds.Bottom - 1 - sourceLocation.Y)), 2));

            // If the farthest point is within range, the source might affect the bounds
            return maxDistanceFromSource <= sourceRange + Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) / 2;
        }

        /// <summary>
        /// Clamps all light levels in the frame to [0.0, 1.0].
        /// Multiple sources can contribute, but we cap at 1.0 for brightness.
        /// </summary>
        private void ClampLightLevels(LightFrame lightFrame)
        {
            var locations = lightFrame.LightLevels.Keys.ToList();
            foreach (var location in locations)
            {
                var currentLevel = lightFrame.GetLightLevel(location);
                if (currentLevel > 1.0)
                    lightFrame.SetLightLevel(location, 1.0);
            }
        }
    }
}


