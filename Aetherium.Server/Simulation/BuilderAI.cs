using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.WorldGen.Prefabs;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Simple AI for NPCs that build structures using prefab blueprints.
    /// Integrates with region grains to place buildings over time.
    /// </summary>
    public class BuilderAI
    {
        private readonly PrefabLibrary _prefabLibrary;
        private readonly PrefabStamper _stamper;
        private readonly Random _random;
        private readonly Dictionary<string, BuildTask> _activeBuildTasks;

        public BuilderAI(PrefabLibrary prefabLibrary)
        {
            _prefabLibrary = prefabLibrary ?? throw new ArgumentNullException(nameof(prefabLibrary));
            _stamper = new PrefabStamper();
            _random = new Random();
            _activeBuildTasks = new Dictionary<string, BuildTask>();
        }

        /// <summary>
        /// Processes build tasks for a region during a tick.
        /// </summary>
        public async Task ProcessBuildTasksAsync(
            IMapRegionGrain region,
            RegionStateSnapshot snapshot,
            World? world,
            double timeOfDay,
            int day)
        {
            if (world == null)
                return;

            // Check if we should start a new build task
            if (_random.NextDouble() < GetBuildProbability(timeOfDay, day))
            {
                await StartBuildTaskAsync(region, snapshot, world);
            }

            // Process any active build tasks in this region
            var regionTasks = _activeBuildTasks.Values
                .Where(t => t.RegionId == snapshot.RegionId)
                .ToList();

            foreach (var task in regionTasks)
            {
                if (task.StartTime.AddDays(GetBuildDuration(task.PrefabId)) < DateTime.UtcNow)
                {
                    // Build task is complete
                    await CompleteBuildTaskAsync(region, task, world);
                    _activeBuildTasks.Remove(task.TaskId);
                }
            }
        }

        /// <summary>
        /// Starts a new build task if conditions are met.
        /// </summary>
        private async Task StartBuildTaskAsync(
            IMapRegionGrain region,
            RegionStateSnapshot snapshot,
            World world)
        {
            // Select a prefab to build
            var prefab = SelectPrefab();
            if (prefab == null)
                return;

            // Find a suitable location
            var location = FindBuildLocation(world, snapshot, prefab);
            if (location == null)
                return;

            var buildLocation = location;

            // Check if we can place the prefab
            if (!_stamper.CanPlace(world, prefab, buildLocation))
                return;

            // Create build task
            var task = new BuildTask
            {
                TaskId = Guid.NewGuid().ToString(),
                RegionId = snapshot.RegionId,
                PrefabId = prefab.PrefabId,
                Location = buildLocation,
                StartTime = DateTime.UtcNow,
                Rotation = _random.Next(0, 4) * 90 // 0, 90, 180, or 270
            };

            _activeBuildTasks[task.TaskId] = task;

            // Record in region snapshot
            // TODO: Store build task in region snapshot for persistence
            Console.WriteLine($"[BuilderAI] Started build task {task.TaskId}: {prefab.Name} at {location} in region {snapshot.RegionId}");
        }

        /// <summary>
        /// Completes a build task by placing the structure.
        /// </summary>
        private async Task CompleteBuildTaskAsync(
            IMapRegionGrain region,
            BuildTask task,
            World world)
        {
            var prefab = await _prefabLibrary.GetPrefabAsync(task.PrefabId);
            if (prefab == null)
            {
                Console.WriteLine($"[BuilderAI] Prefab {task.PrefabId} not found for task {task.TaskId}");
                return;
            }

            // Place the structure
            _stamper.Stamp(world, prefab, task.Location, task.Rotation, blendEdges: true);

            // Record terrain modifications in region snapshot
            // TODO: Update region snapshot with terrain modifications

            Console.WriteLine($"[BuilderAI] Completed build task {task.TaskId}: {prefab.Name} at {task.Location}");
        }

        /// <summary>
        /// Selects a prefab to build based on available blueprints.
        /// </summary>
        private PrefabTemplate? SelectPrefab()
        {
            // Get all building prefabs
            var buildingPrefabs = _prefabLibrary.GetByCategory("building")
                .Where(p => p != null)
                .ToList();

            if (buildingPrefabs.Count == 0)
                return null;

            // Simple selection: pick a random building prefab
            return buildingPrefabs[_random.Next(buildingPrefabs.Count)];
        }

        /// <summary>
        /// Finds a suitable location to build a structure.
        /// </summary>
        private WorldLocation? FindBuildLocation(World world, RegionStateSnapshot snapshot, PrefabTemplate prefab)
        {
            // Simple strategy: find an empty area within the region
            // For now, use a heuristic that looks for relatively empty spaces

            var regionX = snapshot.RegionX;
            var regionY = snapshot.RegionY;
            var regionSize = snapshot.RegionSize;
            var z = snapshot.ZLevel;

            // Start search from center of region
            var centerX = regionX * regionSize + regionSize / 2;
            var centerY = regionY * regionSize + regionSize / 2;

            // Search in a spiral pattern
            var maxRadius = regionSize / 2;
            for (int radius = 0; radius < maxRadius; radius++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        if (Math.Abs(offsetX) != radius && Math.Abs(offsetY) != radius)
                            continue; // Only check perimeter

                        var x = centerX + offsetX;
                        var y = centerY + offsetY;

                    // Check bounds (basic check - world doesn't expose dimensions directly)
                    // We'll rely on CanPlace and terrain checks

                        var location = new WorldLocation(x, y, z);

                        // Check if location is suitable (no existing structures in footprint)
                        if (IsSuitableBuildLocation(world, location, prefab))
                        {
                            return location;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a location is suitable for building (not overlapping existing structures).
        /// </summary>
        private bool IsSuitableBuildLocation(World world, WorldLocation location, PrefabTemplate prefab)
        {
            // Check if prefab can be placed at this location
            if (!_stamper.CanPlace(world, prefab, location))
                return false;

            // Additional checks: ensure surrounding area is relatively clear
            var margin = 2; // Tiles of margin around building
            for (int dy = -margin; dy <= prefab.Height + margin; dy++)
            {
                for (int dx = -margin; dx <= prefab.Width + margin; dx++)
                {
                    var checkLoc = new WorldLocation(location.X + dx, location.Y + dy, location.Z);

                    // Check if there are blocking entities
                    if (world.EntitiesByLocation.TryGetValue(checkLoc, out var entity) && entity != null)
                    {
                        // Don't build if there's an entity too close
                        if (dx >= 0 && dx < prefab.Width && dy >= 0 && dy < prefab.Height)
                            return false; // Entity inside footprint
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the probability of starting a build task (based on time of day, etc.).
        /// </summary>
        private double GetBuildProbability(double timeOfDay, int day)
        {
            // Build more during day time (6 AM - 8 PM)
            if (timeOfDay >= 6.0 && timeOfDay <= 20.0)
            {
                return 0.01; // 1% chance per tick during day
            }

            return 0.005; // 0.5% chance per tick during night
        }

        /// <summary>
        /// Gets the build duration in real-time days for a prefab.
        /// </summary>
        private int GetBuildDuration(string prefabId)
        {
            // Simple heuristic: larger structures take longer
            var prefab = _prefabLibrary.GetPrefabAsync(prefabId).GetAwaiter().GetResult();
            if (prefab == null)
                return 1;

            var size = prefab.Width * prefab.Height;
            if (size < 20)
                return 1; // Small: 1 day
            else if (size < 50)
                return 2; // Medium: 2 days
            else
                return 3; // Large: 3 days
        }
    }

    /// <summary>
    /// Represents an active build task.
    /// </summary>
    internal class BuildTask
    {
        public string TaskId { get; set; } = string.Empty;
        public string RegionId { get; set; } = string.Empty;
        public string PrefabId { get; set; } = string.Empty;
        public WorldLocation Location { get; set; }
        public DateTime StartTime { get; set; }
        public int Rotation { get; set; }
    }
}

