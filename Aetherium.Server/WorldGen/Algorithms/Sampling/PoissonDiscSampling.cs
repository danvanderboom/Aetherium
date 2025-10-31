using System;
using System.Collections.Generic;

namespace Aetherium.WorldGen.Algorithms.Sampling
{
    /// <summary>
    /// Poisson Disc Sampling using Bridson's algorithm for evenly distributed points.
    /// Used for placing buildings, trees, and other features with minimum spacing.
    /// </summary>
    public class PoissonDiscSampling
    {
        private readonly Random _random;
        private readonly double _minDistance;
        private readonly int _width;
        private readonly int _height;
        private readonly int _maxAttempts;

        public PoissonDiscSampling(int width, int height, double minDistance, Random? random = null, int maxAttempts = 30)
        {
            _width = width;
            _height = height;
            _minDistance = minDistance;
            _random = random ?? new Random();
            _maxAttempts = maxAttempts;
        }

        /// <summary>
        /// Generates sample points with minimum distance constraint.
        /// </summary>
        /// <returns>List of (x, y) coordinates</returns>
        public List<(double x, double y)> Generate()
        {
            // Grid cell size for spatial lookup
            double cellSize = _minDistance / Math.Sqrt(2);
            int gridWidth = (int)Math.Ceiling(_width / cellSize);
            int gridHeight = (int)Math.Ceiling(_height / cellSize);
            
            var grid = new int?[gridWidth, gridHeight];
            var points = new List<(double x, double y)>();
            var activeList = new List<int>();

            // Add first point
            double x0 = _random.NextDouble() * _width;
            double y0 = _random.NextDouble() * _height;
            int idx = points.Count;
            points.Add((x0, y0));
            activeList.Add(idx);
            grid[(int)(x0 / cellSize), (int)(y0 / cellSize)] = idx;

            // Process active list
            while (activeList.Count > 0)
            {
                int activeIdx = _random.Next(activeList.Count);
                int pointIdx = activeList[activeIdx];
                var point = points[pointIdx];
                bool found = false;

                // Try to generate new points around this point
                for (int attempt = 0; attempt < _maxAttempts; attempt++)
                {
                    // Random point in annulus between minDistance and 2*minDistance
                    double angle = _random.NextDouble() * 2 * Math.PI;
                    double radius = _minDistance * (1 + _random.NextDouble());
                    double newX = point.x + radius * Math.Cos(angle);
                    double newY = point.y + radius * Math.Sin(angle);

                    // Check if in bounds
                    if (newX < 0 || newX >= _width || newY < 0 || newY >= _height)
                        continue;

                    // Check if far enough from existing points
                    if (IsValidPoint(newX, newY, points, grid, cellSize, gridWidth, gridHeight))
                    {
                        int newIdx = points.Count;
                        points.Add((newX, newY));
                        activeList.Add(newIdx);
                        grid[(int)(newX / cellSize), (int)(newY / cellSize)] = newIdx;
                        found = true;
                        break;
                    }
                }

                // Remove from active list if no valid point found
                if (!found)
                {
                    activeList.RemoveAt(activeIdx);
                }
            }

            return points;
        }

        /// <summary>
        /// Generates sample points within a specific region.
        /// </summary>
        public List<(double x, double y)> GenerateInRegion(
            double regionX, double regionY, double regionWidth, double regionHeight)
        {
            var sampler = new PoissonDiscSampling(
                (int)regionWidth,
                (int)regionHeight,
                _minDistance,
                _random,
                _maxAttempts);
            
            var points = sampler.Generate();
            
            // Offset points to region position
            var offsetPoints = new List<(double x, double y)>();
            foreach (var point in points)
            {
                offsetPoints.Add((point.x + regionX, point.y + regionY));
            }
            
            return offsetPoints;
        }

        /// <summary>
        /// Generates integer coordinate samples (for tile-based placement).
        /// </summary>
        public List<(int x, int y)> GenerateIntPoints()
        {
            var doublePoints = Generate();
            var intPoints = new List<(int x, int y)>();
            
            foreach (var point in doublePoints)
            {
                intPoints.Add(((int)point.x, (int)point.y));
            }
            
            return intPoints;
        }

        private bool IsValidPoint(
            double x, double y,
            List<(double x, double y)> points,
            int?[,] grid,
            double cellSize,
            int gridWidth,
            int gridHeight)
        {
            int gridX = (int)(x / cellSize);
            int gridY = (int)(y / cellSize);

            // Check neighboring cells
            int searchRadius = 2;
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    int checkX = gridX + dx;
                    int checkY = gridY + dy;

                    if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight)
                        continue;

                    int? pointIdx = grid[checkX, checkY];
                    if (pointIdx.HasValue)
                    {
                        var existingPoint = points[pointIdx.Value];
                        double distX = x - existingPoint.x;
                        double distY = y - existingPoint.y;
                        double dist = Math.Sqrt(distX * distX + distY * distY);

                        if (dist < _minDistance)
                            return false;
                    }
                }
            }

            return true;
        }
    }
}


