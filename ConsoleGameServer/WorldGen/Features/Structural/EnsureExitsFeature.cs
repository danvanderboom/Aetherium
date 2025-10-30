using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Features.Structural
{
    /// <summary>
    /// Ensures all buildings have at least one exit (door) to the outside.
    /// Scans for enclosed structures and adds exits if needed.
    /// </summary>
    public class EnsureExitsFeature : IGenerationFeature
    {
        private readonly int _minExits;

        public EnsureExitsFeature(int minExits = 1)
        {
            _minExits = minExits;
        }

        public void Apply(World world, GeneratorContext context)
        {
            // Find all buildings (enclosed indoor spaces)
            var buildings = IdentifyBuildings(world, context);

            foreach (var building in buildings)
            {
                EnsureBuildingHasExits(world, building, context);
            }
        }

        private List<List<WorldLocation>> IdentifyBuildings(World world, GeneratorContext context)
        {
            var buildings = new List<List<WorldLocation>>();
            var visited = new HashSet<WorldLocation>();

            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    var loc = new WorldLocation(x, y, context.ZLevel);
                    
                    if (visited.Contains(loc))
                        continue;

                    if (!world.EntitiesByLocation.ContainsKey(loc))
                        continue;

                    var terrainType = world.GetTerrainType(loc);
                    if (terrainType?.Name == "Indoors")
                    {
                        // Flood fill to find connected indoor spaces
                        var building = FloodFillBuilding(world, loc, context, visited);
                        if (building.Count > 0)
                        {
                            buildings.Add(building);
                        }
                    }
                }
            }

            return buildings;
        }

        private List<WorldLocation> FloodFillBuilding(
            World world,
            WorldLocation start,
            GeneratorContext context,
            HashSet<WorldLocation> globalVisited)
        {
            var building = new List<WorldLocation>();
            var queue = new Queue<WorldLocation>();
            var localVisited = new HashSet<WorldLocation>();

            queue.Enqueue(start);
            localVisited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                building.Add(current);
                globalVisited.Add(current);

                // Check neighbors
                var neighbors = new[]
                {
                    new WorldLocation(current.X + 1, current.Y, current.Z),
                    new WorldLocation(current.X - 1, current.Y, current.Z),
                    new WorldLocation(current.X, current.Y + 1, current.Z),
                    new WorldLocation(current.X, current.Y - 1, current.Z)
                };

                foreach (var neighbor in neighbors)
                {
                    if (localVisited.Contains(neighbor))
                        continue;

                    if (!world.EntitiesByLocation.ContainsKey(neighbor))
                        continue;

                    var terrainType = world.GetTerrainType(neighbor);
                    if (terrainType?.Name == "Indoors")
                    {
                        localVisited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return building;
        }

        private void EnsureBuildingHasExits(World world, List<WorldLocation> building, GeneratorContext context)
        {
            // Find perimeter (indoor cells adjacent to non-indoor)
            var perimeterCandidates = new List<WorldLocation>();

            foreach (var loc in building)
            {
                var neighbors = new[]
                {
                    new WorldLocation(loc.X + 1, loc.Y, loc.Z),
                    new WorldLocation(loc.X - 1, loc.Y, loc.Z),
                    new WorldLocation(loc.X, loc.Y + 1, loc.Z),
                    new WorldLocation(loc.X, loc.Y - 1, loc.Z)
                };

                foreach (var neighbor in neighbors)
                {
                    if (world.EntitiesByLocation.ContainsKey(neighbor))
                    {
                        var terrainType = world.GetTerrainType(neighbor);
                        // If neighbor is not indoor and not wall, this is a valid exit location
                        if (terrainType?.Name != "Indoors" && terrainType?.Name != "Wall")
                        {
                            perimeterCandidates.Add(loc);
                            break;
                        }
                    }
                }
            }

            // If no exits found on perimeter, make one by converting a wall
            if (perimeterCandidates.Count == 0)
            {
                // Find walls adjacent to building
                foreach (var loc in building)
                {
                    var neighbors = new[]
                    {
                        new WorldLocation(loc.X + 1, loc.Y, loc.Z),
                        new WorldLocation(loc.X - 1, loc.Y, loc.Z),
                        new WorldLocation(loc.X, loc.Y + 1, loc.Z),
                        new WorldLocation(loc.X, loc.Y - 1, loc.Z)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (world.EntitiesByLocation.ContainsKey(neighbor))
                        {
                            var terrainType = world.GetTerrainType(neighbor);
                            if (terrainType?.Name == "Wall")
                            {
                                // Convert wall to indoor (door)
                                world.SetTerrain("Indoors", neighbor);
                                perimeterCandidates.Add(neighbor);
                                break;
                            }
                        }
                    }

                    if (perimeterCandidates.Count > 0)
                        break;
                }
            }

            // Ensure minimum number of exits
            // (Currently just ensures at least one - can be extended for multiple exits)
        }
    }
}

