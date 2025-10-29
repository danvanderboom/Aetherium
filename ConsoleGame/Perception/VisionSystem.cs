using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Systems
{
    public class VisionSystem
    {
        private readonly FovCalculator fov = new FovCalculator();

        public VisionFrame ComputeVision(World world, WorldLocation origin, Rectangle bounds, int maxRange)
        {
            var frame = new VisionFrame();

            var visible = fov.ComputeVisible(world, origin, bounds, maxRange);

            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    if (!visible[y, x])
                        continue;

                    var worldLoc = new WorldLocation(bounds.X + x, bounds.Y + y, origin.Z);

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


