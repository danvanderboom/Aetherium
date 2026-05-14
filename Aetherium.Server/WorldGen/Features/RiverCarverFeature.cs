using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen.Features
{
    /// <summary>
    /// Generation feature that carves river paths through terrain.
    /// </summary>
    public class RiverCarverFeature : IGenerationFeature
    {
        // Terrains a river is permitted to flow over. Anything not in this set (walls, doors,
        // building interiors, dungeon features, roads, etc.) is preserved so rivers don't
        // clobber content placed by earlier generation passes.
        private static readonly HashSet<string> CarveableTerrains = new(StringComparer.OrdinalIgnoreCase)
        {
            "Plains",
            "Forest",
            "Mountain",
            "Hills",
            "Water",
            "Sand",
            "Desert",
            "Grass",
            "Dirt"
        };

        // Probability that the river takes a random jitter step instead of advancing toward
        // the target (0 = straight line, 1 = pure random walk).
        private const double RiverJitterProbability = 0.3;

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
            var startEdge = context.GetRandom("feature:river").Next(4); // 0=North, 1=East, 2=South, 3=West
            var (startX, startY) = GetEdgePoint(startEdge, context);

            // Pick end point on opposite or adjacent edge
            var endEdge = (startEdge + 2) % 4; // Opposite edge
            var (endX, endY) = GetEdgePoint(endEdge, context);

            CarveRiver(world, context, startX, startY, endX, endY);
        }

        private void CarveRandomRiver(World world, GeneratorContext context)
        {
            var startX = context.GetRandom("feature:river").Next(context.Width);
            var startY = context.GetRandom("feature:river").Next(context.Height);
            var endX = context.GetRandom("feature:river").Next(context.Width);
            var endY = context.GetRandom("feature:river").Next(context.Height);

            CarveRiver(world, context, startX, startY, endX, endY);
        }

        private void CarveRiver(World world, GeneratorContext context, int startX, int startY, int endX, int endY)
        {
            int x = startX;
            int y = startY;
            var visited = new HashSet<(int, int)>();

            // Random walks can revisit cells, so cap iterations independently of distinct-cell count.
            int maxIterations = context.Width * context.Height * 4;
            int iterations = 0;

            while ((x != endX || y != endY) && iterations < maxIterations)
            {
                iterations++;

                // Emit exactly _width cells per side (asymmetric for even widths).
                int halfLow = _width / 2;
                int halfHigh = _width - 1 - halfLow;
                for (int dy = -halfLow; dy <= halfHigh; dy++)
                {
                    for (int dx = -halfLow; dx <= halfHigh; dx++)
                    {
                        int rx = x + dx;
                        int ry = y + dy;
                        if (rx < 0 || rx >= context.Width || ry < 0 || ry >= context.Height)
                            continue;

                        var loc = new WorldLocation(rx, ry, context.ZLevel);
                        var existing = world.GetTerrainType(loc)?.Name;
                        if (existing != null && !CarveableTerrains.Contains(existing))
                            continue;

                        world.SetTerrain("Water", loc);
                    }
                }

                visited.Add((x, y));

                var directions = new List<(int dx, int dy)>();

                if (x < endX) directions.Add((1, 0));
                if (x > endX) directions.Add((-1, 0));
                if (y < endY) directions.Add((0, 1));
                if (y > endY) directions.Add((0, -1));

                if (context.GetRandom("feature:river").NextDouble() < RiverJitterProbability)
                {
                    directions.Add((0, 1));
                    directions.Add((0, -1));
                    directions.Add((1, 0));
                    directions.Add((-1, 0));
                }

                if (directions.Count > 0)
                {
                    var (dx, dy) = directions[context.GetRandom("feature:river").Next(directions.Count)];
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
                0 => (context.GetRandom("feature:river").Next(context.Width), 0), // North
                1 => (context.Width - 1, context.GetRandom("feature:river").Next(context.Height)), // East
                2 => (context.GetRandom("feature:river").Next(context.Width), context.Height - 1), // South
                3 => (0, context.GetRandom("feature:river").Next(context.Height)), // West
                _ => (context.Width / 2, context.Height / 2)
            };
        }
    }
}


