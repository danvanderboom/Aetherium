using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen.Algorithms.Graphs;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// A simple deterministic generator that creates a bounded map with rooms and corridors.
    /// Uses BSP-like approach: places a few rectangular rooms and connects them with corridors.
    /// Always includes boundaries, a start location, and a light source.
    /// </summary>
    public class RoomsAndCorridorsGenerator : IMapGenerator
    {
        // Extra-edge ratio passed to the MST: the fraction of additional non-tree edges added
        // to create loops. ~20% gives some loop-back paths without flooding the layout.
        private const double MstExtraEdgeRatio = 0.2;

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

            // Scoped RNG: keep rooms-and-corridors RNG independent from other passes/features
            // that share the same GeneratorContext.
            var rng = context.GetRandom("rooms-and-corridors");

            // Fill entire area with walls (boundaries)
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, context.ZLevel));
                }
            }

            // Generate 3-5 rooms
            var numRooms = rng.Next(3, 6);
            var rooms = new List<Rectangle>();

            for (int i = 0; i < numRooms; i++)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    var roomWidth = rng.Next(4, 8);
                    var roomHeight = rng.Next(4, 8);
                    var x = rng.Next(1, Math.Max(2, context.Width - roomWidth - 1));
                    var y = rng.Next(1, Math.Max(2, context.Height - roomHeight - 1));

                    var room = new Rectangle(x, y, roomWidth, roomHeight);

                    // Inflate by 1 before overlap test so adjacent rooms can't share a wall edge.
                    var inflated = Rectangle.Inflate(room, 1, 1);
                    if (rooms.Any(r => inflated.IntersectsWith(r)))
                        continue;

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

            // Connect rooms via MST + a small fraction of extra edges (for loop variety).
            // Each room becomes an MST node at its center; MST guarantees full connectivity
            // without the serpentine artifacts of i→i+1 pairing.
            ConnectRoomsViaMST(world, rooms, rng, context.ZLevel);

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

        private static void ConnectRoomsViaMST(World world, List<Rectangle> rooms, Random rng, int z)
        {
            if (rooms.Count < 2)
                return;

            var nodes = rooms
                .Select(r => new MinimumSpanningTree.Node(r.X + r.Width / 2, r.Y + r.Height / 2))
                .ToList();

            var edges = MinimumSpanningTree.ComputeMSTWithExtraEdges(nodes, MstExtraEdgeRatio, rng);

            foreach (var edge in edges)
            {
                CarveLCorridor(world, edge.From.X, edge.From.Y, edge.To.X, edge.To.Y, z);
            }
        }

        private static void CarveLCorridor(World world, int x1, int y1, int x2, int y2, int z)
        {
            int startX = Math.Min(x1, x2);
            int endX = Math.Max(x1, x2);
            for (int cx = startX; cx <= endX; cx++)
            {
                world.SetTerrain("Indoors", new WorldLocation(cx, y1, z));
            }

            int startY = Math.Min(y1, y2);
            int endY = Math.Max(y1, y2);
            for (int cy = startY; cy <= endY; cy++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x2, cy, z));
            }
        }
    }
}


