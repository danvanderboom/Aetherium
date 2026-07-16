using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// Hex-native cave generator (docs/grid-topologies.md P1 polish): cellular-automata caves
    /// carved on the hexagonal lattice itself, so passages and chambers follow six-way adjacency
    /// instead of a square grid re-read as hexagons. The map is a hex-shaped disc centered at
    /// (width/2, height/2) in axial coordinates; cells outside the disc are never created (void),
    /// and the disc rim is always Wall. Requires a world whose topology is "hex" — the CA
    /// neighborhood, connectivity flood-fill, and disc shape are all six-way.
    /// </summary>
    public class HexCavesGenerator : IMapGenerator, ITopologyAwareGenerator
    {
        public IReadOnlyCollection<string> SupportedTopologies { get; } = new[] { "hex" };

        private readonly TestMazeWorldBuilder _baseBuilder = new();

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            var tileTypes = _baseBuilder.TileTypes;
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(_baseBuilder.CreateTerrainTypes(tileTypes));

            double fillProbability = GetParameter(context, "fillProbability", 0.45);
            int smoothingIterations = GetParameter(context, "smoothingIterations", 4);

            var hex = HexTopology.Instance;
            var center = new GridCoord(context.Width / 2, context.Height / 2, context.ZLevel);
            int radius = Math.Max(2, Math.Min(context.Width, context.Height) / 2 - 1);

            var rng = context.GetRandom("hex-caves");

            // Seed: random wall/floor inside the disc, forced wall on the rim so the caves
            // never open into the void beyond the disc.
            var isWall = new Dictionary<GridCoord, bool>();
            foreach (var cell in hex.Range(center, radius))
                isWall[cell] = hex.Distance(center, cell) == radius || rng.NextDouble() < fillProbability;

            // Cellular-automata smoothing over the six hex neighbors. Missing neighbors (off-disc)
            // count as walls, which keeps the rim solid and rounds off chambers near the edge.
            for (int i = 0; i < smoothingIterations; i++)
            {
                var next = new Dictionary<GridCoord, bool>(isWall.Count);
                foreach (var (cell, _) in isWall)
                {
                    if (hex.Distance(center, cell) == radius)
                    {
                        next[cell] = true;
                        continue;
                    }
                    int wallNeighbors = hex.Neighbors(cell).Count(n => !isWall.TryGetValue(n, out var w) || w);
                    next[cell] = wallNeighbors >= 4 || (wallNeighbors == 3 && isWall[cell]);
                }
                isWall = next;
            }

            // Keep only the largest connected floor region; seal the rest. A disconnected pocket
            // is unreachable and would strand spawns.
            var largest = LargestFloorRegion(hex, isWall);
            if (largest.Count == 0)
            {
                // Degenerate (tiny map or unlucky seed): carve a minimal chamber at the center.
                isWall[center] = false;
                foreach (var n in hex.Neighbors(center))
                    if (isWall.ContainsKey(n))
                        isWall[n] = false;
                largest = LargestFloorRegion(hex, isWall);
            }
            foreach (var (cell, wall) in isWall)
                if (!wall && !largest.Contains(cell))
                    isWall[cell] = true;

            foreach (var (cell, wall) in isWall)
                world.SetTerrain(wall ? "Wall" : "Cave", new WorldLocation(cell.X, cell.Y, cell.Z));

            // Start at the floor cell nearest the disc center, with a light source (caves are dark).
            var start = largest.OrderBy(c => hex.Distance(center, c)).First();
            context.StartLocation = new WorldLocation(start.X, start.Y, start.Z);
            var light = new LightEntity();
            light.Set(new LightSource(1.0, 50));
            light.Set(context.StartLocation);
            world.AddEntity(light);

            return world;
        }

        private static HashSet<GridCoord> LargestFloorRegion(HexTopology hex, Dictionary<GridCoord, bool> isWall)
        {
            var unvisited = new HashSet<GridCoord>(isWall.Where(kv => !kv.Value).Select(kv => kv.Key));
            var largest = new HashSet<GridCoord>();
            var queue = new Queue<GridCoord>();
            while (unvisited.Count > largest.Count)
            {
                var seed = unvisited.First();
                var region = new HashSet<GridCoord> { seed };
                unvisited.Remove(seed);
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    foreach (var n in hex.Neighbors(queue.Dequeue()))
                    {
                        if (unvisited.Remove(n))
                        {
                            region.Add(n);
                            queue.Enqueue(n);
                        }
                    }
                }
                if (region.Count > largest.Count)
                    largest = region;
            }
            return largest;
        }

        private static double GetParameter(GeneratorContext context, string key, double defaultValue)
            => context.GeneratorParams != null
               && context.GeneratorParams.TryGetValue(key, out var value)
               && double.TryParse(value, out var parsed)
                ? parsed
                : defaultValue;

        private static int GetParameter(GeneratorContext context, string key, int defaultValue)
            => context.GeneratorParams != null
               && context.GeneratorParams.TryGetValue(key, out var value)
               && int.TryParse(value, out var parsed)
                ? parsed
                : defaultValue;
    }
}
