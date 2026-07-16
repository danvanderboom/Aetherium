using System;
using System.Drawing;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Systems
{
    /// <summary>
    /// Extends FOV calculation with directional filtering based on heading and field-of-view angle.
    /// Filters omnidirectional visible cells to only those within a forward-facing cone.
    /// </summary>
    public class DirectionalFovCalculator
    {
        private readonly FovCalculator baseFovCalculator = new FovCalculator();

        /// <summary>
        /// Computes visible cells within a directional cone.
        /// </summary>
        /// <param name="world">The game world</param>
        /// <param name="origin">The observer's location</param>
        /// <param name="bounds">The bounding rectangle to compute visibility within</param>
        /// <param name="maxRange">Maximum visibility range</param>
        /// <param name="headingDegrees">Facing direction in degrees (0=North, 90=East, 180=South, 270=West)</param>
        /// <param name="fovDegrees">Field of view angle in degrees (e.g., 120 for human-like vision)</param>
        /// <returns>2D array indicating which cells are visible</returns>
        public bool[,] ComputeVisible(World world, WorldLocation origin, Rectangle bounds, int maxRange, 
            int headingDegrees, int fovDegrees)
        {
            // If FOV is 360 or greater, no directional filtering needed
            if (fovDegrees >= 360)
            {
                return baseFovCalculator.ComputeVisible(world, origin, bounds, maxRange);
            }

            // First, compute omnidirectional visibility
            var omnidirectionalVisible = baseFovCalculator.ComputeVisible(world, origin, bounds, maxRange);

            // Then, filter to only cells within the directional cone
            return FilterByCone(world, omnidirectionalVisible, origin, bounds, headingDegrees, fovDegrees);
        }

        /// <summary>
        /// Filters a visibility grid to only include cells within a directional cone.
        /// </summary>
        private bool[,] FilterByCone(World world, bool[,] omnidirectionalVisible, WorldLocation origin, Rectangle bounds,
            int headingDegrees, int fovDegrees)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var filtered = new bool[height, width];

            // Convert heading to radians and compute the heading vector
            // In our coordinate system: North is -Y, South is +Y, East is +X, West is -X
            double headingRadians = headingDegrees * Math.PI / 180.0;
            double headingVectorX = Math.Sin(headingRadians);   // East is positive X
            double headingVectorY = -Math.Cos(headingRadians);  // North is negative Y (decreasing)

            // Half-angle of the cone in radians
            double halfFovRadians = (fovDegrees / 2.0) * Math.PI / 180.0;
            double cosHalfFov = Math.Cos(halfFovRadians);

            // Process each cell
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Skip if not visible in omnidirectional FOV
                    if (!omnidirectionalVisible[y, x])
                        continue;

                    // Calculate world coordinates
                    int worldX = bounds.X + x;
                    int worldY = bounds.Y + y;

                    // Vector from origin to cell in the topology's local embedding —
                    // on square, exactly the raw coordinate difference. The heading
                    // vector below is already in these +X-east/+Y-south axes, so the
                    // cone dot-product stays valid on any planar topology.
                    var (dx, dy) = world.Topology.Delta(
                        Aetherium.Topology.GridCoord.From(origin),
                        new Aetherium.Topology.GridCoord(worldX, worldY, origin.Z));

                    // Skip the origin cell itself (always visible)
                    if (dx == 0 && dy == 0)
                    {
                        filtered[y, x] = true;
                        continue;
                    }

                    // Normalize the direction vector
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < 1e-9)
                    {
                        // Too close to origin, include it
                        filtered[y, x] = true;
                        continue;
                    }

                    double dirX = dx / distance;
                    double dirY = dy / distance;

                    // Calculate dot product to determine angle
                    // dot = |heading| * |dir| * cos(angle) = cos(angle) since both are unit vectors
                    double dotProduct = headingVectorX * dirX + headingVectorY * dirY;

                    // If the dot product is >= cos(halfFov), the cell is within the cone
                    if (dotProduct >= cosHalfFov)
                    {
                        filtered[y, x] = true;
                    }
                }
            }

            return filtered;
        }
    }
}


