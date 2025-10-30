using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Lighting;

namespace ConsoleGame.Systems
{
    public class VisionSystem
    {
        private readonly FovCalculator fov = new FovCalculator();
        private readonly LightingSystem lightingSystem = new LightingSystem();

        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            return ComputeVision(world, origin, bounds, maxRange, null);
        }

        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange, LightFrame? lightFrame)
        {
            var frame = new VisionFrame();

            // Compute lighting if not provided
            if (lightFrame == null)
            {
                lightFrame = lightingSystem.ComputeLighting(world, bounds, origin.Z);
            }

            // Adjust maxRange based on light at origin
            // In darkness (light < 0.1), visibility range is reduced
            var originLightLevel = lightFrame.GetLightLevel(origin);
            var effectiveRange = maxRange;
            if (originLightLevel < 0.1)
            {
                // Reduce range significantly in darkness
                effectiveRange = (int)(maxRange * originLightLevel * 10.0);
                effectiveRange = Math.Max(1, effectiveRange); // Always see at least 1 cell
            }

            var visible = fov.ComputeVisible(world, origin, bounds, effectiveRange);

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    if (!visible[y, x])
                        continue;

                    var worldLoc = new WorldLocation(bounds.X + x, bounds.Y + y, origin.Z);

                    // Additional check: even if FOV says visible, require minimum light level
                    var lightLevel = lightFrame.GetLightLevel(worldLoc);
                    if (lightLevel < 0.05) // Almost completely dark
                    {
                        // Only allow visibility if very close (within 2 cells)
                        var dx = worldLoc.X - origin.X;
                        var dy = worldLoc.Y - origin.Y;
                        var distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance > 2.0)
                            continue;
                    }

                    var terrainType = world.GetTerrainType(worldLoc);
                    var visual = new Visual(worldLoc, terrainType?.TileType);

                    // Record what is present for future consumers
                    if (world.EntitiesByLocation.TryGetValue(worldLoc, out var entities))
                    {
                        foreach (var entity in entities.Values)
                        {
                            if (entity is Character)
                            {
                                AddSeen(visual, VisualType.Character, "count", 1);
                            }
                            else if (!(entity is Entities.Terrain))
                            {
                                AddSeen(visual, VisualType.Object, "count", 1);
                            }
                        }
                    }

                    frame.AddVisual(visual);
                }
            }

            return frame;
        }

        private static void AddSeen(Visual visual, VisualType type, string key, double value)
        {
            if (!visual.ThingsSeen.TryGetValue(type, out var metrics))
            {
                metrics = new Dictionary<string, double>();
                visual.ThingsSeen[type] = metrics;
            }

            if (metrics.ContainsKey(key))
                metrics[key] += value;
            else
                metrics[key] = value;
        }
    }
}


