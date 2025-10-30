using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGameServer.Perception
{
    /// <summary>
    /// Tracks heat trails left by entities with HeatSignature components.
    /// Heat trails fade over time based on the entity's heat duration setting.
    /// </summary>
    public class HeatTrailTracker
    {
        private struct HeatTrail
        {
            public string EntityId;
            public DateTime Timestamp;
            public double BaseIntensity;
            public TimeSpan Duration;
        }

        // WorldLocation -> List of heat trails at that location
        private readonly ConcurrentDictionary<WorldLocation, List<HeatTrail>> heatTrails;

        public HeatTrailTracker()
        {
            heatTrails = new ConcurrentDictionary<WorldLocation, List<HeatTrail>>();
        }

        /// <summary>
        /// Records an entity's heat signature at a location
        /// </summary>
        public void RecordEntityPosition(Entity entity, WorldLocation location, DateTime timestamp)
        {
            var heatSig = entity.Get<HeatSignature>();
            if (heatSig == null || heatSig.Intensity <= 0.0)
                return;

            var trail = new HeatTrail
            {
                EntityId = entity.EntityId,
                Timestamp = timestamp,
                BaseIntensity = heatSig.Intensity,
                Duration = heatSig.Duration
            };

            heatTrails.AddOrUpdate(
                location,
                new List<HeatTrail> { trail },
                (key, existing) =>
                {
                    // Remove old trails from this entity at this location
                    existing.RemoveAll(t => t.EntityId == trail.EntityId);
                    existing.Add(trail);
                    return existing;
                });
        }

        /// <summary>
        /// Gets the total heat intensity at a location, accounting for fading over time
        /// </summary>
        public double GetHeatAtLocation(WorldLocation location, DateTime currentTime)
        {
            if (!heatTrails.TryGetValue(location, out var trails))
                return 0.0;

            double totalHeat = 0.0;

            foreach (var trail in trails)
            {
                var elapsed = currentTime - trail.Timestamp;
                if (elapsed < TimeSpan.Zero)
                    elapsed = TimeSpan.Zero;

                if (elapsed >= trail.Duration)
                    continue; // Trail has completely faded

                // Linear fade: intensity decreases from base to 0 over duration
                var fadeFactor = 1.0 - (elapsed.TotalSeconds / trail.Duration.TotalSeconds);
                var fadedIntensity = trail.BaseIntensity * fadeFactor;
                
                totalHeat += fadedIntensity;
            }

            return Math.Min(totalHeat, 1.0); // Cap at 1.0
        }

        /// <summary>
        /// Removes all heat trails older than the cutoff time
        /// </summary>
        public void CleanupOldTrails(DateTime cutoffTime)
        {
            var locationsToRemove = new List<WorldLocation>();

            foreach (var kvp in heatTrails)
            {
                var location = kvp.Key;
                var trails = kvp.Value;

                // Remove trails that have expired
                trails.RemoveAll(t => (cutoffTime - t.Timestamp) >= t.Duration);

                // If no trails remain at this location, mark for removal
                if (trails.Count == 0)
                    locationsToRemove.Add(location);
            }

            // Remove empty locations
            foreach (var location in locationsToRemove)
            {
                heatTrails.TryRemove(location, out _);
            }
        }

        /// <summary>
        /// Gets all locations that currently have heat signatures
        /// </summary>
        public IEnumerable<WorldLocation> GetActiveLocations(DateTime currentTime)
        {
            var activeLocations = new List<WorldLocation>();

            foreach (var kvp in heatTrails)
            {
                if (GetHeatAtLocation(kvp.Key, currentTime) > 0.0)
                    activeLocations.Add(kvp.Key);
            }

            return activeLocations;
        }

        /// <summary>
        /// Clears all heat trails (useful for testing or resetting state)
        /// </summary>
        public void Clear()
        {
            heatTrails.Clear();
        }
    }
}

