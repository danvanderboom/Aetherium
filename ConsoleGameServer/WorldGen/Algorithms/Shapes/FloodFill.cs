using System;
using System.Collections.Generic;
using ConsoleGame.Components;

namespace ConsoleGame.WorldGen.Algorithms.Shapes
{
    /// <summary>
    /// Flood fill algorithm for creating organic blob shapes (lakes, forests, city blocks).
    /// Uses BFS for predictable expansion.
    /// </summary>
    public static class FloodFill
    {
        /// <summary>
        /// Performs flood fill starting from a seed location, expanding to neighbors.
        /// </summary>
        /// <param name="seed">Starting location</param>
        /// <param name="maxSize">Maximum number of locations to fill</param>
        /// <param name="canExpand">Predicate determining if a location can be filled</param>
        /// <param name="random">Random for non-deterministic expansion</param>
        /// <returns>List of locations in the filled region</returns>
        public static List<WorldLocation> Fill(
            WorldLocation seed,
            int maxSize,
            Func<WorldLocation, bool> canExpand,
            Random? random = null)
        {
            var filled = new List<WorldLocation>();
            var visited = new HashSet<WorldLocation>();
            var queue = new Queue<WorldLocation>();
            
            queue.Enqueue(seed);
            visited.Add(seed);
            
            while (queue.Count > 0 && filled.Count < maxSize)
            {
                var current = queue.Dequeue();
                
                if (!canExpand(current))
                    continue;
                
                filled.Add(current);
                
                // Get neighbors in random order if RNG provided
                var neighbors = GetNeighbors(current);
                if (random != null)
                {
                    Shuffle(neighbors, random);
                }
                
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            return filled;
        }

        /// <summary>
        /// Fills a circular region with given radius.
        /// </summary>
        public static List<WorldLocation> FillCircle(
            WorldLocation center,
            int radius,
            int z = 0)
        {
            var filled = new List<WorldLocation>();
            int radiusSquared = radius * radius;
            
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        filled.Add(new WorldLocation(center.X + dx, center.Y + dy, z));
                    }
                }
            }
            
            return filled;
        }

        /// <summary>
        /// Fills an elliptical region.
        /// </summary>
        public static List<WorldLocation> FillEllipse(
            WorldLocation center,
            int radiusX,
            int radiusY,
            int z = 0)
        {
            var filled = new List<WorldLocation>();
            int radiusXSquared = radiusX * radiusX;
            int radiusYSquared = radiusY * radiusY;
            
            for (int dy = -radiusY; dy <= radiusY; dy++)
            {
                for (int dx = -radiusX; dx <= radiusX; dx++)
                {
                    double normalized = (double)(dx * dx) / radiusXSquared + (double)(dy * dy) / radiusYSquared;
                    if (normalized <= 1.0)
                    {
                        filled.Add(new WorldLocation(center.X + dx, center.Y + dy, z));
                    }
                }
            }
            
            return filled;
        }

        /// <summary>
        /// Fills a rectangular region.
        /// </summary>
        public static List<WorldLocation> FillRectangle(
            WorldLocation topLeft,
            int width,
            int height,
            int z = 0)
        {
            var filled = new List<WorldLocation>();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    filled.Add(new WorldLocation(topLeft.X + x, topLeft.Y + y, z));
                }
            }
            
            return filled;
        }

        private static List<WorldLocation> GetNeighbors(WorldLocation location)
        {
            return new List<WorldLocation>
            {
                new WorldLocation(location.X + 1, location.Y, location.Z),
                new WorldLocation(location.X - 1, location.Y, location.Z),
                new WorldLocation(location.X, location.Y + 1, location.Z),
                new WorldLocation(location.X, location.Y - 1, location.Z)
            };
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

