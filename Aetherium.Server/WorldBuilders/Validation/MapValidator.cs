using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.WorldBuilders.Validation
{
    /// <summary>
    /// Validates maps against configurable standards, checking boundaries, lighting, start locations, etc.
    /// </summary>
    public sealed class MapValidator
    {
        /// <summary>
        /// Validates a world map according to the provided options.
        /// </summary>
        public MapValidationReport Validate(World world, MapValidationOptions options)
        {
            var report = new MapValidationReport();

            // Check that terrain types are registered
            ValidateTerrainTypes(world, options.ZLevel, report);

            // Check boundaries
            if (options.RequireExplicitBoundary)
            {
                ValidateBoundaries(world, options.ZLevel, report);
            }

            // Check for light sources
            if (options.RequireLightSource)
            {
                ValidateLightSources(world, options.ZLevel, report);
            }

            // Validate start location if provided
            if (options.StartLocation is not null)
            {
                ValidateStartLocation(world, options.StartLocation, options.MinReachableLocations, report);
            }

            return report;
        }

        private void ValidateTerrainTypes(World world, int zLevel, MapValidationReport report)
        {
            // Get all terrain entities at the specified Z-level
            var terrainsAtZ = world.EntitiesByLocation
                .Where(kvp => kvp.Key.Z == zLevel)
                .SelectMany(kvp => kvp.Value.Values.OfType<Terrain>())
                .ToList();

            var uniqueTerrainNames = terrainsAtZ
                .Select(t => t.Type.Name)
                .Distinct()
                .ToList();

            // Check that all terrain names are registered
            foreach (var terrainName in uniqueTerrainNames)
            {
                if (!world.TerrainTypes.ContainsKey(terrainName))
                {
                    report.AddError(
                        "TerrainTypes",
                        $"Terrain type '{terrainName}' is used but not registered in World.TerrainTypes",
                        $"Z={zLevel}");
                }
            }
        }

        private void ValidateBoundaries(World world, int zLevel, MapValidationReport report)
        {
            // Get all passable locations at the specified Z-level
            var passableLocations = world.EntitiesByLocation.Keys
                .Where(loc => loc.Z == zLevel && world.PassableTerrain(loc))
                .ToHashSet();

            if (passableLocations.Count == 0)
            {
                report.AddError("Boundary", "No passable terrain found at the specified Z-level", $"Z={zLevel}");
                return;
            }

            // Calculate bounding box
            var minX = passableLocations.Min(loc => loc.X);
            var maxX = passableLocations.Max(loc => loc.X);
            var minY = passableLocations.Min(loc => loc.Y);
            var maxY = passableLocations.Max(loc => loc.Y);

            // Check all passable locations for boundary violations
            var boundaryErrors = new List<string>();
            
            foreach (var loc in passableLocations)
            {
                // Check if this is at the edge of the bounding box
                bool isAtXEdge = loc.X == minX || loc.X == maxX;
                bool isAtYEdge = loc.Y == minY || loc.Y == maxY;

                if (!isAtXEdge && !isAtYEdge)
                    continue; // Interior location, skip

                // Check neighbors
                var neighbors = new[]
                {
                    new WorldLocation(loc.X - 1, loc.Y, zLevel), // West
                    new WorldLocation(loc.X + 1, loc.Y, zLevel), // East
                    new WorldLocation(loc.X, loc.Y - 1, zLevel), // North
                    new WorldLocation(loc.X, loc.Y + 1, zLevel)  // South
                };

                foreach (var neighbor in neighbors)
                {
                    // If neighbor is outside bounding box, it must not exist (implicit boundary)
                    // or be impassable (explicit boundary)
                    bool neighborInBounds = neighbor.X >= minX && neighbor.X <= maxX &&
                                          neighbor.Y >= minY && neighbor.Y <= maxY;

                    if (!neighborInBounds)
                    {
                        // Outside bounding box ⇒ at map edge. For explicit boundaries, this MUST
                        // be blocked by an explicit impassable tile (e.g., Wall). If the neighbor
                        // cell does not exist, that's an implicit boundary and is a violation here.
                        if (!world.EntitiesByLocation.ContainsKey(neighbor))
                        {
                            boundaryErrors.Add($"({loc.X},{loc.Y},{zLevel})");
                            break;
                        }
                        else if (world.PassableTerrain(neighbor))
                        {
                            // Neighbor exists but is passable at the edge → violation
                            boundaryErrors.Add($"({loc.X},{loc.Y},{zLevel})");
                            break;
                        }
                    }
                    else if (world.PassableTerrain(neighbor))
                    {
                        // Neighbor is passable and within bounds - that's fine
                        continue;
                    }
                }

                // For locations at the very edge, check that at least one cardinal direction
                // leads to impassable or non-existent terrain (indicating a boundary)
                if (isAtXEdge || isAtYEdge)
                {
                    var hasBoundaryNeighbor = neighbors.Any(neighbor =>
                    {
                        if (!world.EntitiesByLocation.ContainsKey(neighbor))
                            return true; // Non-existent = implicit boundary

                        if (!world.PassableTerrain(neighbor))
                            return true; // Impassable = explicit boundary

                        return false;
                    });

                    if (!hasBoundaryNeighbor)
                    {
                        // All neighbors are passable and exist - no boundary detected
                        if (!boundaryErrors.Contains($"({loc.X},{loc.Y},{zLevel})"))
                        {
                            boundaryErrors.Add($"({loc.X},{loc.Y},{zLevel})");
                        }
                    }
                }
            }

            if (boundaryErrors.Count > 0)
            {
                var locationsList = string.Join(", ", boundaryErrors.Take(10));
                if (boundaryErrors.Count > 10)
                    locationsList += $", ... ({boundaryErrors.Count - 10} more)";

                report.AddError(
                    "Boundary",
                    $"Passable terrain found at map edges without proper boundaries: {locationsList}",
                    $"Z={zLevel}");
            }
        }

        private void ValidateLightSources(World world, int zLevel, MapValidationReport report)
        {
            // Find all entities with LightSource components at the specified Z-level
            var lightSources = world.Entities.Values
                .Where(entity =>
                {
                    if (!entity.Has<WorldLocation>())
                        return false;

                    var location = entity.Get<WorldLocation>();
                    if (location == null || location.Z != zLevel)
                        return false;

                    if (!entity.Has<LightSource>())
                        return false;

                    var lightSource = entity.Get<LightSource>();
                    return lightSource != null && lightSource.IsEnabled;
                })
                .ToList();

            if (lightSources.Count == 0)
            {
                report.AddError(
                    "Lighting",
                    $"No enabled light sources found at Z-level {zLevel}",
                    $"Z={zLevel}");
            }
        }

        private void ValidateStartLocation(World world, WorldLocation? startLocation, int? minReachableLocations, MapValidationReport report)
        {
            // Guard against null or None (avoid operator== overload by using 'is')
            if (startLocation is null || startLocation.IsNone)
                return; // Skip validation if start location is not provided
            
            // Check that start location exists in the world
            if (!world.EntitiesByLocation.ContainsKey(startLocation))
            {
                report.AddError(
                    "StartLocation",
                    "Start location does not exist in the world",
                    startLocation.ToString());
                return;
            }

            // Check that start location is passable
            if (!world.PassableTerrain(startLocation))
            {
                report.AddError(
                    "StartLocation",
                    "Start location is not passable",
                    startLocation.ToString());
                return;
            }

            // If minimum reachable locations is specified, check reachability
            if (minReachableLocations.HasValue)
            {
                var reachableCount = CountReachableLocations(world, startLocation);
                if (reachableCount < minReachableLocations.Value)
                {
                    report.AddError(
                        "StartLocation",
                        $"Only {reachableCount} locations are reachable from start location, minimum is {minReachableLocations.Value}",
                        startLocation.ToString());
                }
            }
        }

        private int CountReachableLocations(World world, WorldLocation startLocation)
        {
            // BFS to count all reachable passable locations
            var visited = new HashSet<WorldLocation>();
            var queue = new Queue<WorldLocation>();
            queue.Enqueue(startLocation);
            visited.Add(startLocation);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // Check cardinal neighbors
                var neighbors = new[]
                {
                    new WorldLocation(current.X, current.Y - 1, current.Z), // North
                    new WorldLocation(current.X, current.Y + 1, current.Z), // South
                    new WorldLocation(current.X - 1, current.Y, current.Z), // West
                    new WorldLocation(current.X + 1, current.Y, current.Z)  // East
                };

                foreach (var neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    if (!world.EntitiesByLocation.ContainsKey(neighbor))
                        continue;

                    if (!world.PassableTerrain(neighbor))
                        continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return visited.Count;
        }
    }
}


