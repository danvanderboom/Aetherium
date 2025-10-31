using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen.Algorithms.Shapes;

namespace Aetherium.WorldGen.Features
{
    /// <summary>
    /// Places clusters of terrain or prefabs (forest groves, lakes, etc.).
    /// </summary>
    public class ClusterFeature : IGenerationFeature
    {
        private readonly string _terrainType;
        private readonly int _count;
        private readonly int _minSize;
        private readonly int _maxSize;
        private readonly string _placementTerrain;

        public ClusterFeature(
            string terrainType,
            int count = 5,
            int minSize = 5,
            int maxSize = 15,
            string placementTerrain = "Plains")
        {
            _terrainType = terrainType;
            _count = count;
            _minSize = minSize;
            _maxSize = maxSize;
            _placementTerrain = placementTerrain;
        }

        public void Apply(World world, GeneratorContext context)
        {
            // Find suitable locations for cluster centers
            var candidates = new List<WorldLocation>();
            
            for (int y = 10; y < context.Height - 10; y++)
            {
                for (int x = 10; x < context.Width - 10; x++)
                {
                    var loc = new WorldLocation(x, y, context.ZLevel);
                    if (world.EntitiesByLocation.ContainsKey(loc))
                    {
                        // Check if this is a valid placement location
                        var terrain = world.GetTerrain(loc);
                        if (terrain != null)
                        {
                            var terrainType = world.GetTerrainType(loc);
                            if (terrainType?.Name == _placementTerrain)
                            {
                                candidates.Add(loc);
                            }
                        }
                    }
                }
            }

            // Place clusters
            int placedCount = 0;
            int attempts = 0;
            int maxAttempts = _count * 10;

            while (placedCount < _count && attempts < maxAttempts)
            {
                attempts++;
                
                if (candidates.Count == 0)
                    break;

                var centerLoc = candidates[context.Random.Next(candidates.Count)];
                int size = context.Random.Next(_minSize, _maxSize + 1);

                // Use FloodFill or shape for organic cluster
                List<WorldLocation> clusterLocs;
                
                if (context.Random.NextDouble() < 0.5)
                {
                    // Circular cluster
                    int radius = size / 2;
                    clusterLocs = FloodFill.FillCircle(centerLoc, radius, context.ZLevel);
                }
                else
                {
                    // Elliptical cluster
                    int radiusX = context.Random.Next(size / 2, size);
                    int radiusY = context.Random.Next(size / 2, size);
                    clusterLocs = FloodFill.FillEllipse(centerLoc, radiusX, radiusY, context.ZLevel);
                }

                // Apply terrain to cluster
                foreach (var loc in clusterLocs)
                {
                    if (world.EntitiesByLocation.ContainsKey(loc))
                    {
                        world.SetTerrain(_terrainType, loc);
                    }
                }

                placedCount++;

                // Remove nearby candidates to avoid overlapping clusters
                candidates.RemoveAll(c =>
                {
                    int dx = c.X - centerLoc.X;
                    int dy = c.Y - centerLoc.Y;
                    return dx * dx + dy * dy < size * size;
                });
            }
        }
    }
}


