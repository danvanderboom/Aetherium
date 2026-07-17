using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server;
using World = Aetherium.Core.World;
using TileType = Aetherium.Core.TileType;
using TerrainType = Aetherium.Core.TerrainType;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// On a featureless open floor, the torch's perception pool must be symmetric around
    /// the player — no direction has anything to occlude, so any lopsidedness is a bug in
    /// the light/FOV/bounds pipeline (seen live as a pool clipped to the south and west).
    /// </summary>
    [TestFixture]
    public class TorchPoolSymmetryTests
    {
        private static (World world, WorldLocation player) OpenFloorWorld()
        {
            var world = new World();
            var tile = new TileType { Name = "Indoors", Settings = new Dictionary<string, string>() };
            world.AddTileTypes(new List<TileType> { tile });
            world.AddTerrainTypes(new List<TerrainType>
            {
                new TerrainType { Name = "Indoors", TileType = tile },
            });

            // A floor big enough that the viewport never clips the lamp radius.
            for (var y = -20; y <= 20; y++)
                for (var x = -20; x <= 20; x++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));

            return (world, new WorldLocation(0, 0, 0));
        }

        private static HashSet<(int X, int Y)> VisibleRelCells(System.Drawing.Size viewport)
        {
            var (world, player) = OpenFloorWorld();
            var perception = new PerceptionService().ComputePerception(
                world, player, Aetherium.WorldDirection.North, viewport, self: null);

            var cells = new HashSet<(int, int)>();
            foreach (var key in perception.Visuals.Keys)
            {
                var parts = key.Split(',');
                cells.Add((int.Parse(parts[0]), int.Parse(parts[1])));
            }
            return cells;
        }

        [Test]
        public void TorchPool_OnOpenFloor_IsFourWaySymmetric()
        {
            var cells = VisibleRelCells(new System.Drawing.Size(40, 40));

            Assert.That(cells, Does.Contain((0, 0)), "the player's own cell is visible");
            Assert.That(cells.Count, Is.GreaterThan(20), "a torch pool has real area");

            var asymmetries = new List<string>();
            foreach (var (x, y) in cells)
            {
                foreach (var mirror in new[] { (-x, y), (x, -y), (-x, -y) })
                {
                    if (!cells.Contains(mirror))
                        asymmetries.Add($"({x},{y}) visible but mirror {mirror} is not");
                }
            }

            Assert.That(asymmetries, Is.Empty,
                "open-floor torch pool must be symmetric; asymmetric cells:\n  " +
                string.Join("\n  ", asymmetries.Take(40)) +
                $"\n  ({asymmetries.Count} total)\n" +
                DrawPool(cells));
        }

        /// <summary>Renders the pool as ASCII (# visible, . not) for failure diagnosis.</summary>
        private static string DrawPool(HashSet<(int X, int Y)> cells)
        {
            var minX = cells.Min(c => c.X); var maxX = cells.Max(c => c.X);
            var minY = cells.Min(c => c.Y); var maxY = cells.Max(c => c.Y);
            var rows = new List<string>();
            for (var y = minY; y <= maxY; y++)
            {
                var row = "";
                for (var x = minX; x <= maxX; x++)
                    row += x == 0 && y == 0 ? "@" : cells.Contains((x, y)) ? "#" : ".";
                rows.Add(row);
            }
            return $"pool x:[{minX},{maxX}] y:[{minY},{maxY}]\n" + string.Join("\n", rows);
        }
    }
}
