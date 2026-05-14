using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen;

namespace Aetherium.WorldGen.Features.Structural
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
            // Existing exits: indoor cells that already touch traversable outside terrain
            // (anything that isn't Indoors/Wall/None — Plains, Forest, Road, etc.).
            var existingExits = new HashSet<WorldLocation>();
            foreach (var loc in building)
            {
                if (HasOutsideNeighbor(world, loc))
                    existingExits.Add(loc);
            }

            if (existingExits.Count >= _minExits)
                return;

            // Order building cells deterministically so exit placement is seed-stable.
            var deterministic = building.OrderBy(l => l.Z).ThenBy(l => l.Y).ThenBy(l => l.X).ToList();

            // Carve additional exits by converting walls adjacent to *traversable outside* terrain.
            // Prefer walls facing real terrain (Plains/Road/etc.) over walls facing the void.
            int needed = _minExits - existingExits.Count;
            var carved = new HashSet<WorldLocation>();
            foreach (var loc in deterministic)
            {
                if (needed <= 0)
                    break;
                foreach (var dir in WorldLocationNeighbors.Cardinal4Offsets)
                {
                    var wallLoc = new WorldLocation(loc.X + dir.dx, loc.Y + dir.dy, loc.Z);
                    if (carved.Contains(wallLoc))
                        continue;
                    if (!world.EntitiesByLocation.ContainsKey(wallLoc))
                        continue;
                    if (world.GetTerrainType(wallLoc)?.Name != "Wall")
                        continue;

                    // The wall must face traversable outside terrain on its far side.
                    var beyond = new WorldLocation(wallLoc.X + dir.dx, wallLoc.Y + dir.dy, wallLoc.Z);
                    if (!IsTraversableOutside(world, beyond))
                        continue;

                    world.SetTerrain("Indoors", wallLoc);
                    carved.Add(wallLoc);
                    needed--;
                    if (needed <= 0)
                        break;
                }
            }

            // Last-resort fallback: convert any adjacent wall, even if it faces the void.
            // Better than leaving the building sealed.
            if (needed > 0)
            {
                foreach (var loc in deterministic)
                {
                    if (needed <= 0)
                        break;
                    foreach (var dir in WorldLocationNeighbors.Cardinal4Offsets)
                    {
                        var wallLoc = new WorldLocation(loc.X + dir.dx, loc.Y + dir.dy, loc.Z);
                        if (carved.Contains(wallLoc))
                            continue;
                        if (!world.EntitiesByLocation.ContainsKey(wallLoc))
                            continue;
                        if (world.GetTerrainType(wallLoc)?.Name != "Wall")
                            continue;
                        world.SetTerrain("Indoors", wallLoc);
                        carved.Add(wallLoc);
                        needed--;
                        if (needed <= 0)
                            break;
                    }
                }
            }
        }

        private static bool HasOutsideNeighbor(World world, WorldLocation loc)
        {
            foreach (var n in WorldLocationNeighbors.Cardinal4(loc))
            {
                if (!world.EntitiesByLocation.ContainsKey(n))
                    continue;
                var t = world.GetTerrainType(n)?.Name;
                if (t != null && t != "Indoors" && t != "Wall" && t != "None")
                    return true;
            }
            return false;
        }

        private static bool IsTraversableOutside(World world, WorldLocation loc)
        {
            if (!world.EntitiesByLocation.ContainsKey(loc))
                return false;
            var t = world.GetTerrainType(loc)?.Name;
            return t != null && t != "Indoors" && t != "Wall" && t != "None";
        }
    }
}


