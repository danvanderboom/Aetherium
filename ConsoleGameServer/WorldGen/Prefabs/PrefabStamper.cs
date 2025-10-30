using System;
using System.Collections.Generic;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Prefabs
{
    /// <summary>
    /// Stamps prefab templates into a world at specified locations with optional rotation.
    /// </summary>
    public class PrefabStamper
    {
        /// <summary>
        /// Places a prefab into the world at the specified location.
        /// </summary>
        /// <param name="world">Target world</param>
        /// <param name="prefab">Prefab template to stamp</param>
        /// <param name="location">Top-left corner location</param>
        /// <param name="rotation">Rotation in degrees (0, 90, 180, 270)</param>
        /// <param name="blendEdges">If true, blends terrain at prefab edges</param>
        public void Stamp(
            World world,
            PrefabTemplate prefab,
            WorldLocation location,
            int rotation = 0,
            bool blendEdges = false)
        {
            if (rotation % 90 != 0)
                throw new ArgumentException("Rotation must be multiple of 90 degrees", nameof(rotation));

            rotation = (rotation % 360 + 360) % 360; // Normalize to 0-359

            // Get dimensions after rotation
            int width = prefab.Width;
            int height = prefab.Height;
            if (rotation == 90 || rotation == 270)
            {
                (width, height) = (height, width);
            }

            // Stamp tiles
            for (int y = 0; y < prefab.Height; y++)
            {
                for (int x = 0; x < prefab.Width; x++)
                {
                    var tile = prefab.Tiles[x, y];
                    if (tile == null)
                        continue;

                    // Calculate rotated position
                    var (rotX, rotY) = RotatePoint(x, y, prefab.Width, prefab.Height, rotation);
                    var worldLoc = new WorldLocation(
                        location.X + rotX,
                        location.Y + rotY,
                        location.Z);

                    // Set terrain
                    if (!string.IsNullOrEmpty(tile.TerrainType))
                    {
                        world.SetTerrain(tile.TerrainType, worldLoc);
                    }

                    // Spawn entity if specified
                    if (!string.IsNullOrEmpty(tile.EntityType))
                    {
                        SpawnEntity(world, tile, worldLoc);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a prefab can be placed at the specified location without overlap.
        /// </summary>
        public bool CanPlace(
            World world,
            PrefabTemplate prefab,
            WorldLocation location,
            int rotation = 0)
        {
            rotation = (rotation % 360 + 360) % 360;

            for (int y = 0; y < prefab.Height; y++)
            {
                for (int x = 0; x < prefab.Width; x++)
                {
                    var (rotX, rotY) = RotatePoint(x, y, prefab.Width, prefab.Height, rotation);
                    var worldLoc = new WorldLocation(
                        location.X + rotX,
                        location.Y + rotY,
                        location.Z);

                    // Check if location exists and is available
                    if (!world.EntitiesByLocation.ContainsKey(worldLoc))
                        return false;

                    // Optional: Add more sophisticated overlap detection
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the bounding box of a rotated prefab.
        /// </summary>
        public (int width, int height) GetRotatedDimensions(PrefabTemplate prefab, int rotation)
        {
            rotation = (rotation % 360 + 360) % 360;
            
            if (rotation == 90 || rotation == 270)
            {
                return (prefab.Height, prefab.Width);
            }
            
            return (prefab.Width, prefab.Height);
        }

        private (int x, int y) RotatePoint(int x, int y, int width, int height, int rotation)
        {
            return rotation switch
            {
                0 => (x, y),
                90 => (y, width - 1 - x),
                180 => (width - 1 - x, height - 1 - y),
                270 => (height - 1 - y, x),
                _ => (x, y)
            };
        }

        private void SpawnEntity(World world, PrefabTile tile, WorldLocation location)
        {
            // Basic entity spawning - can be extended based on EntityType
            // For now, this is a placeholder that would be implemented
            // with actual entity factories based on game needs

            // Example:
            // switch (tile.EntityType)
            // {
            //     case "Door":
            //         var door = new Door();
            //         door.Set(location);
            //         world.AddEntity(door);
            //         break;
            //     // ... other entity types
            // }

            Console.WriteLine($"[PrefabStamper] Would spawn {tile.EntityType} at {location}");
        }
    }
}

