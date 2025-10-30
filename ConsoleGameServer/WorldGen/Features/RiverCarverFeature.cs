using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen.Features
{
    /// <summary>
    /// Generation feature that carves river paths through terrain.
    /// </summary>
    public class RiverCarverFeature : IGenerationFeature
    {
        private readonly int _width;
        private readonly bool _connectEdges;

        public RiverCarverFeature(int width = 3, bool connectEdges = true)
        {
            _width = width;
            _connectEdges = connectEdges;
        }

        public void Apply(World world, GeneratorContext context)
        {
            if (_connectEdges)
            {
                // Carve river from one edge to another
                CarveEdgeToEdgeRiver(world, context);
            }
            else
            {
                // Carve river from random start to random end
                CarveRandomRiver(world, context);
            }
        }

        private void CarveEdgeToEdgeRiver(World world, GeneratorContext context)
        {
            // Pick random start point on one edge
            var startEdge = context.Random.Next(4); // 0=North, 1=East, 2=South, 3=West
            var (startX, startY) = GetEdgePoint(startEdge, context);

            // Pick end point on opposite or adjacent edge
            var endEdge = (startEdge + 2) % 4; // Opposite edge
            var (endX, endY) = GetEdgePoint(endEdge, context);

            CarveRiver(world, context, startX, startY, endX, endY);
        }

        private void CarveRandomRiver(World world, GeneratorContext context)
        {
            var startX = context.Random.Next(context.Width);
            var startY = context.Random.Next(context.Height);
            var endX = context.Random.Next(context.Width);
            var endY = context.Random.Next(context.Height);

            CarveRiver(world, context, startX, startY, endX, endY);
        }

        private void CarveRiver(World world, GeneratorContext context, int startX, int startY, int endX, int endY)
        {
            // Simple random walk with bias toward target
            int x = startX;
            int y = startY;
            var visited = new HashSet<(int, int)>();

            while ((x != endX || y != endY) && visited.Count < context.Width * context.Height)
            {
                // Widen river at current position
                for (int dy = -_width / 2; dy <= _width / 2; dy++)
                {
                    for (int dx = -_width / 2; dx <= _width / 2; dx++)
                    {
                        int rx = x + dx;
                        int ry = y + dy;
                        if (rx >= 0 && rx < context.Width && ry >= 0 && ry < context.Height)
                        {
                            var loc = new WorldLocation(rx, ry, context.ZLevel);
                            world.SetTerrain("Water", loc);
                        }
                    }
                }

                visited.Add((x, y));

                // Move toward target with some randomness
                var directions = new List<(int dx, int dy)>();
                
                if (x < endX) directions.Add((1, 0));
                if (x > endX) directions.Add((-1, 0));
                if (y < endY) directions.Add((0, 1));
                if (y > endY) directions.Add((0, -1));

                // Add orthogonal directions for variety
                if (context.Random.NextDouble() < 0.3)
                {
                    directions.Add((0, 1));
                    directions.Add((0, -1));
                    directions.Add((1, 0));
                    directions.Add((-1, 0));
                }

                if (directions.Count > 0)
                {
                    var (dx, dy) = directions[context.Random.Next(directions.Count)];
                    x += dx;
                    y += dy;
                    x = Math.Clamp(x, 0, context.Width - 1);
                    y = Math.Clamp(y, 0, context.Height - 1);
                }
                else
                {
                    break;
                }
            }
        }

        private (int x, int y) GetEdgePoint(int edge, GeneratorContext context)
        {
            return edge switch
            {
                0 => (context.Random.Next(context.Width), 0), // North
                1 => (context.Width - 1, context.Random.Next(context.Height)), // East
                2 => (context.Random.Next(context.Width), context.Height - 1), // South
                3 => (0, context.Random.Next(context.Height)), // West
                _ => (context.Width / 2, context.Height / 2)
            };
        }
    }
}

