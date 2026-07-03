using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Lighting;

namespace Aetherium.Server.Perception
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
        /// When <paramref name="heatFrame"/> is provided, each heated location's
        /// intensity is also recorded there as its "light" level — infrared
        /// repurposes <c>VisualDto.LightLevel</c> as the heat channel, and the
        /// clients color by it (see ClientConsoleMapView.GetInfraredColor).
        /// Without this the DTO conversion reads an empty frame, every visual
        /// ships with LightLevel = 0, and infrared renders a black map.
        /// </summary>
        public VisionFrame ComputeHeatVision(
            World world,
            WorldLocation playerLocation,
            Rectangle bounds,
            HeatTrailTracker heatTracker,
            DateTime currentTime,
            LightFrame? heatFrame = null)
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
                            heatFrame?.SetLightLevel(location, heatLevel);
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
                    // Component.Get<T>() throws when the component is absent, so use
                    // the safe OfType lookup — most entities have no HeatSignature.
                    var heatSig = entity.AllComponents.OfType<HeatSignature>().FirstOrDefault();
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
                // Get the tile type from the terrain entity (safe lookup — Get<T> throws when absent)
                var tile = terrain.AllComponents.OfType<Tile>().FirstOrDefault();
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
                    var tile = entity.AllComponents.OfType<Tile>().FirstOrDefault();
                    if (tile != null && tile.Type != null)
                    {
                        // Note: In infrared mode, we're showing heat signatures
                        // The Visual structure will be interpreted differently by the client

                        // Add visual type markers
                        if (entity is Aetherium.Character)
                        {
                            if (!visual.ThingsSeen.ContainsKey(Aetherium.VisualType.Character))
                                visual.ThingsSeen[Aetherium.VisualType.Character] = new Dictionary<string, double>();
                            visual.ThingsSeen[Aetherium.VisualType.Character]["heat"] = heatLevel;
                        }
                        else
                        {
                            if (!visual.ThingsSeen.ContainsKey(Aetherium.VisualType.Object))
                                visual.ThingsSeen[Aetherium.VisualType.Object] = new Dictionary<string, double>();
                            visual.ThingsSeen[Aetherium.VisualType.Object]["heat"] = heatLevel;
                        }
                    }
                }
            }

            return visual;
        }
    }
}


