using System;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Spatial
{
    /// <summary>
    /// Helper methods for grid coordinate transformations and spatial operations.
    /// </summary>
    public static class GridHelpers
    {
        /// <summary>
        /// Converts a grid cell coordinate to Unity world position.
        /// </summary>
        public static Vector3 GridToWorld(int x, int y, float cellSize = 1.0f)
        {
            return new Vector3(x * cellSize, y * cellSize, 0);
        }

        /// <summary>
        /// Converts a WorldLocationLite to Unity world position.
        /// </summary>
        public static Vector3 GridToWorld(WorldLocationLite location, float cellSize = 1.0f)
        {
            return GridToWorld(location.X, location.Y, cellSize);
        }

        /// <summary>
        /// Converts a Unity world position to grid cell coordinates.
        /// </summary>
        public static (int x, int y) WorldToGrid(Vector3 worldPos, float cellSize = 1.0f)
        {
            return ((int)Mathf.Floor(worldPos.x / cellSize), (int)Mathf.Floor(worldPos.y / cellSize));
        }

        /// <summary>
        /// Gets a key string for a grid location (used in Dictionary lookups).
        /// </summary>
        public static string GetLocationKey(int x, int y, int z)
        {
            return $"{x},{y},{z}";
        }

        /// <summary>
        /// Gets a key string for a WorldLocationLite.
        /// </summary>
        public static string GetLocationKey(WorldLocationLite location)
        {
            return GetLocationKey(location.X, location.Y, location.Z);
        }

        /// <summary>
        /// Gets neighbor coordinates in cardinal directions.
        /// </summary>
        public static (int x, int y)[] GetNeighbors(int x, int y)
        {
            return new[]
            {
                (x, y + 1),     // North
                (x + 1, y),     // East
                (x, y - 1),     // South
                (x - 1, y)      // West
            };
        }
    }
}

