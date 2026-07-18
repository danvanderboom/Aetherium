using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Lighting;

namespace Aetherium.Systems
{
    public class VisionSystem
    {
        private readonly FovCalculator fov = new FovCalculator();
        private readonly DirectionalFovCalculator directionalFov = new DirectionalFovCalculator();
        private readonly LightingSystem lightingSystem = new LightingSystem();

        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            return ComputeVision(world, origin, bounds, maxRange, null);
        }

        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange, LightFrame? lightFrame)
        {
            return ComputeVision(world, origin, bounds, maxRange, lightFrame, null, null);
        }

        /// <summary>
        /// Computes vision with optional directional filtering.
        /// </summary>
        /// <param name="world">The game world</param>
        /// <param name="origin">Observer's location</param>
        /// <param name="bounds">Bounding rectangle</param>
        /// <param name="maxRange">Maximum visibility range</param>
        /// <param name="lightFrame">Pre-computed lighting (optional)</param>
        /// <param name="headingDegrees">Facing direction in degrees (null = omnidirectional)</param>
        /// <param name="fovDegrees">Field of view angle (null = omnidirectional)</param>
        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange, 
            LightFrame? lightFrame, int? headingDegrees, int? fovDegrees)
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

            // Use directional FOV if heading and FOV are specified
            bool[,] visible;
            if (headingDegrees.HasValue && fovDegrees.HasValue && fovDegrees.Value < 360)
            {
                visible = directionalFov.ComputeVisible(world, origin, bounds, effectiveRange, 
                    headingDegrees.Value, fovDegrees.Value);
            }
            else
            {
                visible = fov.ComputeVisible(world, origin, bounds, effectiveRange);
            }

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    if (!visible[y, x])
                        continue;

                    var worldLoc = new WorldLocation(bounds.X + x, bounds.Y + y, origin.Z);

                    // Additional check: even if FOV says visible, require minimum light level
                    // Note: Only apply dark filtering if there are actual light sources in the world.
                    // If no light sources exist, show all FOV-visible cells (for testing scenarios).
                    var lightLevel = lightFrame.GetLightLevel(worldLoc);
                    
                    // Check if there are any light sources in the world by seeing if any location has light > 0
                    // (simpler: if origin has no light and no other cells have light, assume no light sources and don't filter)
                    bool hasLightSources = originLightLevel > 0.001 || lightFrame.LightLevels.Count > 0;
                    
                    if (hasLightSources && lightLevel < 0.05)
                    {
                        // Very dark cell in a world with light sources - only allow visibility if very close (within 2 cells).
                        // Euclidean distance in the topology's local embedding (Delta); on square, the raw difference used before.
                        var (dx, dy) = world.Topology.Delta(
                            Aetherium.Topology.GridCoord.From(origin),
                            Aetherium.Topology.GridCoord.From(worldLoc));
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

            // 3D occluded slab: for each horizontally-visible column, march up/down through the configured band
            // range and emit off-focus cells that pass the vertical line-of-sight test. Only cells that actually
            // contain something (terrain or a non-terrain entity) are emitted — empty air is a silhouette gap, not
            // a visible cell — which keeps off-focus perception cheap.
            int depthBelow = Math.Min(world.SlabDepthBelow, world.SlabDepthCap);
            int depthAbove = Math.Min(world.SlabDepthAbove, world.SlabDepthCap);
            if (depthBelow > 0 || depthAbove > 0)
            {
                for (int y = 0; y < bounds.Height; y++)
                {
                    for (int x = 0; x < bounds.Width; x++)
                    {
                        if (!visible[y, x])
                            continue;

                        int wx = bounds.X + x;
                        int wy = bounds.Y + y;

                        foreach (var z in fov.VerticalVisibleBands(world, wx, wy, origin.Z, depthBelow, depthAbove))
                        {
                            var loc = new WorldLocation(wx, wy, z);
                            var terrainType = world.GetTerrainType(loc);

                            var hasContent = terrainType != null;
                            if (world.EntitiesByLocation.TryGetValue(loc, out var slabEntities))
                            {
                                var visual = new Visual(loc, terrainType?.TileType);
                                foreach (var entity in slabEntities.Values)
                                {
                                    if (entity is Character)
                                    {
                                        AddSeen(visual, VisualType.Character, "count", 1);
                                        hasContent = true;
                                    }
                                    else if (!(entity is Entities.Terrain))
                                    {
                                        AddSeen(visual, VisualType.Object, "count", 1);
                                        hasContent = true;
                                    }
                                }

                                if (hasContent)
                                    frame.AddVisual(visual);
                            }
                            else if (hasContent)
                            {
                                frame.AddVisual(new Visual(loc, terrainType?.TileType));
                            }
                        }
                    }
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



