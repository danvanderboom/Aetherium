using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGameServer.Perception
{
    /// <summary>
    /// Computes infrared/thermal vision perception based on heat signatures.
    /// Entities with HeatSignature components emit heat, and heat trails show movement history.
    /// </summary>
    public class InfraredVisionSystem
    {
        /// <summary>
        /// Computes a VisionFrame based on heat signatures rather than light.
        /// Heat vision ignores lighting and shows thermal emissions.
        /// </summary>
        public VisionFrame ComputeHeatVision(
            World world,
            WorldLocation playerLocation,
            Rectangle bounds,
            HeatTrailTracker heatTracker,
            DateTime currentTime)
        {
            var visionFrame = new VisionFrame();

            // Process each location in bounds
            for (int by = bounds.Top; by < bounds.Bottom; by++)
            {
                for (int bx = bounds.Left; bx < bounds.Right; bx++)
                {
                    var location = new WorldLocation(bx, by, playerLocation.Z);

                    // Get heat at this location (from current entities + trails)
                    double heatLevel = GetHeatAtLocation(world, location, heatTracker, currentTime);

                    // Only show locations with significant heat
                    if (heatLevel > 0.05) // Threshold to reduce noise
                    {
                        // Create visual for this heated location
                        var visual = CreateHeatVisual(world, location, heatLevel);
                        if (visual != null)
                        {
                            visionFrame.AddVisual(visual);
                        }
                    }
                }
            }

            return visionFrame;
        }

        /// <summary>
        /// Gets the total heat intensity at a location from current entities and heat trails
        /// </summary>
        private double GetHeatAtLocation(
            World world,
            WorldLocation location,
            HeatTrailTracker heatTracker,
            DateTime currentTime)
        {
            double totalHeat = 0.0;

            // Check for entities with heat signatures at this location
            if (world.EntitiesByLocation.TryGetValue(location, out var entities))
            {
                foreach (var entity in entities.Values)
                {
                    var heatSig = entity.Get<HeatSignature>();
                    if (heatSig != null)
                    {
                        totalHeat += heatSig.Intensity;
                    }
                }
            }

            // Add heat from trails (faded signatures from entities that passed through)
            double trailHeat = heatTracker.GetHeatAtLocation(location, currentTime);
            totalHeat += trailHeat;

            return Math.Min(totalHeat, 1.0); // Cap at 1.0
        }

        /// <summary>
        /// Creates a visual representation for a location with heat signature
        /// </summary>
        private Visual? CreateHeatVisual(World world, WorldLocation location, double heatLevel)
        {
            // Check if there's terrain at this location
            var visual = new Visual
            {
                Location = location
            };

            // Store heat level in visual (used by client for color mapping)
            // In infrared mode, the LightLevel field is repurposed to store heat level
            // This will be interpreted differently by the client renderer

            // Check for terrain
            var terrain = world.GetTerrain(location);
            if (terrain != null)
            {
                // Get the tile type from the terrain entity
                var tile = terrain.Get<Tile>();
                if (tile != null && tile.Type != null)
                {
                    visual.Terrain = tile.Type;
                }
            }

            // Check for entities at this location
            if (world.EntitiesByLocation.TryGetValue(location, out var entities))
            {
                foreach (var entity in entities.Values)
                {
                    var tile = entity.Get<Tile>();
                    if (tile != null && tile.Type != null)
                    {
                        // Note: In infrared mode, we're showing heat signatures
                        // The Visual structure will be interpreted differently by the client

                        // Add visual type markers
                        if (entity is ConsoleGame.Character)
                        {
                            if (!visual.ThingsSeen.ContainsKey(ConsoleGame.VisualType.Character))
                                visual.ThingsSeen[ConsoleGame.VisualType.Character] = new Dictionary<string, double>();
                            visual.ThingsSeen[ConsoleGame.VisualType.Character]["heat"] = heatLevel;
                        }
                        else
                        {
                            if (!visual.ThingsSeen.ContainsKey(ConsoleGame.VisualType.Object))
                                visual.ThingsSeen[ConsoleGame.VisualType.Object] = new Dictionary<string, double>();
                            visual.ThingsSeen[ConsoleGame.VisualType.Object]["heat"] = heatLevel;
                        }
                    }
                }
            }

            return visual;
        }
    }
}

