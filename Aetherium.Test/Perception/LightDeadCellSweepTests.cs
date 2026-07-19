using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server;
using Aetherium.WorldGen;
using World = Aetherium.Core.World;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Hunts "light-dead cells": positions where the torch pool collapses to the bare
    /// darkness fallback (radius ~2) even though the surrounding area is open floor.
    /// Observed live: stepping one cell shrank inViewCount 44 → 25 with extents
    /// W2 E2 N2 S2 — total light failure at a specific position. This sweep computes
    /// perception from every passable cell of a generated world and reports offenders
    /// with a local map for dissection.
    /// </summary>
    [TestFixture]
    public class LightDeadCellSweepTests
    {
        [Test]
        public void NoOpenCell_HasACollapsedTorchPool()
        {
            var generator = new Aetherium.WorldGen.Generators.RoomsAndCorridorsGenerator();
            SweepWorld(generator.Generate(new GeneratorContext(30, 30, seed: 1234)), 30);
        }

        [Test]
        public void NoOpenCell_HasACollapsedTorchPool_AtAphelionScale()
        {
            // The live collapse happened in an aphelion world (maze, 72x72). Sweep the same
            // generator at the same scale across a few seeds.
            foreach (var seed in new[] { 1, 42, 2026 })
            {
                var generator = new Aetherium.WorldGen.MazeGenerator();
                SweepWorld(generator.Generate(new GeneratorContext(72, 72, seed: seed)), 72, $"seed {seed}: ");
            }
        }

        private static void SweepWorld(World world, int size, string label = "")
        {

            var perception = new PerceptionService();
            var offenders = new List<string>();
            var checkedCells = 0;

            for (var y = 1; y < size - 1; y++)
            {
                for (var x = 1; x < size - 1; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrainType(loc);
                    if (terrain?.TileType == null || terrain.TileType.Name == "Wall")
                        continue;

                    // Only sweep genuinely open spots: at least 12 non-wall cells in the
                    // 5x5 neighborhood, so corridor pinches don't false-positive.
                    var openNeighbors = 0;
                    for (var dy = -2; dy <= 2; dy++)
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var n = world.GetTerrainType(new WorldLocation(x + dx, y + dy, 0));
                            if (n?.TileType != null && n.TileType.Name != "Wall")
                                openNeighbors++;
                        }
                    if (openNeighbors < 12)
                        continue;

                    checkedCells++;
                    var frame = perception.ComputePerception(
                        world, loc, Aetherium.WorldDirection.North,
                        new System.Drawing.Size(42, 22), self: null);

                    // A healthy torch pool on semi-open floor is well above the darkness
                    // fallback (~13-25 cells). Collapse = every extent <= 2.
                    var cells = frame.Visuals.Keys
                        .Select(k => k.Split(','))
                        .Where(p => int.Parse(p[2]) == 0)
                        .Select(p => (X: int.Parse(p[0]), Y: int.Parse(p[1])))
                        .ToList();
                    var west = -cells.Min(c => c.X);
                    var east = cells.Max(c => c.X);
                    var north = -cells.Min(c => c.Y);
                    var south = cells.Max(c => c.Y);

                    if (west <= 2 && east <= 2 && north <= 2 && south <= 2)
                        offenders.Add($"{label}({x},{y}) extents W{west} E{east} N{north} S{south} " +
                                      $"count={cells.Count} openNeighbors={openNeighbors}\n" +
                                      LocalMap(world, x, y));
                }
            }

            Assert.That(checkedCells, Is.GreaterThan(20), label + "the sweep must cover real open area");
            Assert.That(offenders, Is.Empty,
                $"{label}light-dead cells found ({offenders.Count} of {checkedCells} open cells):\n" +
                string.Join("\n", offenders.Take(5)));
        }

        private static string LocalMap(World world, int cx, int cy)
        {
            var rows = new List<string>();
            for (var y = cy - 4; y <= cy + 4; y++)
            {
                var row = "  ";
                for (var x = cx - 4; x <= cx + 4; x++)
                {
                    if (x == cx && y == cy) { row += "@"; continue; }
                    var t = world.GetTerrainType(new WorldLocation(x, y, 0));
                    var name = t?.TileType?.Name;
                    row += name == null ? "?" : name == "Wall" ? "#" : ".";
                }
                rows.Add(row);
            }
            return string.Join("\n", rows);
        }
    }
}
