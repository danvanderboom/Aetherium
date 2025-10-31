using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// A simple deterministic generator that creates a bounded map with rooms and corridors.
    /// Uses BSP-like approach: places a few rectangular rooms and connects them with corridors.
    /// Always includes boundaries, a start location, and a light source.
    /// </summary>
    public class RoomsAndCorridorsGenerator : IMapGenerator
    {
        private readonly WorldBuilder _baseBuilder;

        public RoomsAndCorridorsGenerator()
        {
            // Use TestMazeWorldBuilder as base for tile/terrain types
            _baseBuilder = new TestMazeWorldBuilder();
        }

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            // Cast to TestMazeWorldBuilder to access TileTypes property
            if (_baseBuilder is TestMazeWorldBuilder testBuilder)
            {
                var tileTypes = testBuilder.TileTypes;
                world.AddTileTypes(tileTypes);
                world.AddTerrainTypes(testBuilder.CreateTerrainTypes(tileTypes));
            }
            else
            {
                throw new InvalidOperationException("Expected TestMazeWorldBuilder");
            }

            // Fill entire area with walls (boundaries)
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, context.ZLevel));
                }
            }

            // Generate 3-5 rooms
            var numRooms = context.Random.Next(3, 6);
            var rooms = new List<Rectangle>();

            for (int i = 0; i < numRooms; i++)
            {
                // Try to place a room
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    var roomWidth = context.Random.Next(4, 8);
                    var roomHeight = context.Random.Next(4, 8);
                    var x = context.Random.Next(1, context.Width - roomWidth - 1);
                    var y = context.Random.Next(1, context.Height - roomHeight - 1);

                    var room = new Rectangle(x, y, roomWidth, roomHeight);

                    // Check if room overlaps with existing rooms
                    bool overlaps = rooms.Any(r => room.IntersectsWith(r));
                    if (overlaps)
                        continue;

                    // Place the room
                    rooms.Add(room);
                    for (int ry = y; ry < y + roomHeight; ry++)
                    {
                        for (int rx = x; rx < x + roomWidth; rx++)
                        {
                            world.SetTerrain("Indoors", new WorldLocation(rx, ry, context.ZLevel));
                        }
                    }
                    break;
                }
            }

            // Connect rooms with corridors
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                var room1 = rooms[i];
                var room2 = rooms[i + 1];

                var center1 = new Point(room1.X + room1.Width / 2, room1.Y + room1.Height / 2);
                var center2 = new Point(room2.X + room2.Width / 2, room2.Y + room2.Height / 2);

                // L-shaped corridor
                // Horizontal first
                int startX = Math.Min(center1.X, center2.X);
                int endX = Math.Max(center1.X, center2.X);
                for (int cx = startX; cx <= endX; cx++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(cx, center1.Y, context.ZLevel));
                }

                // Then vertical
                int startY = Math.Min(center1.Y, center2.Y);
                int endY = Math.Max(center1.Y, center2.Y);
                for (int cy = startY; cy <= endY; cy++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(center2.X, cy, context.ZLevel));
                }
            }

            // Set start location to center of first room
            if (rooms.Count > 0)
            {
                var firstRoom = rooms[0];
                var startX = firstRoom.X + firstRoom.Width / 2;
                var startY = firstRoom.Y + firstRoom.Height / 2;
                context.StartLocation = new WorldLocation(startX, startY, context.ZLevel);

                // Add light source at start location
                var lightEntity = new LightEntity();
                lightEntity.Set(new LightSource(1.0, 50));
                lightEntity.Set(context.StartLocation);
                world.AddEntity(lightEntity);
            }
            else
            {
                // Fallback: center of map
                context.StartLocation = new WorldLocation(context.Width / 2, context.Height / 2, context.ZLevel);
            }

            return world;
        }
    }
}


