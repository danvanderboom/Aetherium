using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Features;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class RiverCarverTests
    {
        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static (World world, GeneratorContext ctx) BuildOutdoorWorld(int width, int height, int seed)
        {
            var ctx = new GeneratorContext(width, height, seed);
            var builder = new TestMazeWorldBuilder();
            var world = new World();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Fill with Plains — a terrain the river is allowed to carve.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    world.SetTerrain("Plains", new WorldLocation(x, y, 0));

            return (world, ctx);
        }

        private static string? TerrainAt(World world, WorldLocation loc)
            => world.GetTerrainType(loc)?.Name;

        // ──────────────────────────────────────────────────────────────────────
        // Never overwrites protected terrains
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void RiverCarver_DoesNotOverwriteIndoorsTerrain()
        {
            var (world, ctx) = BuildOutdoorWorld(60, 60, 1);

            // Mark a strip of tiles as Indoors (building interior).
            for (int x = 0; x < 60; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 30, 0));

            var feature = new RiverCarverFeature(width: 5, connectEdges: true);
            feature.Apply(world, ctx);

            // None of the Indoors tiles should have been overwritten.
            for (int x = 0; x < 60; x++)
            {
                var terrain = TerrainAt(world, new WorldLocation(x, 30, 0));
                Assert.That(terrain, Is.EqualTo("Indoors"),
                    $"RiverCarver overwrote Indoors at ({x}, 30)");
            }
        }

        [Test]
        public void RiverCarver_DoesNotOverwriteWallTerrain()
        {
            var (world, ctx) = BuildOutdoorWorld(60, 60, 2);

            // Mark a strip as Wall.
            for (int y = 0; y < 60; y++)
                world.SetTerrain("Wall", new WorldLocation(20, y, 0));

            var feature = new RiverCarverFeature(width: 3, connectEdges: true);
            feature.Apply(world, ctx);

            for (int y = 0; y < 60; y++)
            {
                var terrain = TerrainAt(world, new WorldLocation(20, y, 0));
                Assert.That(terrain, Is.EqualTo("Wall"),
                    $"RiverCarver overwrote Wall at (20, {y})");
            }
        }

        [Test]
        public void RiverCarver_DoesNotOverwriteRoadTerrain()
        {
            var (world, ctx) = BuildOutdoorWorld(60, 60, 3);

            // Roads are a common "important" terrain that rivers should not erase.
            for (int x = 0; x < 60; x++)
                world.SetTerrain("Road", new WorldLocation(x, 40, 0));

            var feature = new RiverCarverFeature(width: 3, connectEdges: false);
            feature.Apply(world, ctx);

            for (int x = 0; x < 60; x++)
            {
                var terrain = TerrainAt(world, new WorldLocation(x, 40, 0));
                Assert.That(terrain, Is.EqualTo("Road"),
                    $"RiverCarver overwrote Road at ({x}, 40)");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // River actually writes Water somewhere
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void RiverCarver_PlacesSomeWaterTiles()
        {
            var (world, ctx) = BuildOutdoorWorld(60, 60, 42);
            var feature = new RiverCarverFeature(width: 3, connectEdges: true);
            feature.Apply(world, ctx);

            int waterCount = 0;
            for (int y = 0; y < 60; y++)
                for (int x = 0; x < 60; x++)
                    if (TerrainAt(world, new WorldLocation(x, y, 0)) == "Water")
                        waterCount++;

            Assert.That(waterCount, Is.GreaterThan(0),
                "RiverCarver should write at least one Water tile.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Determinism
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void RiverCarver_SameSeed_ProducesSamePattern()
        {
            static List<WorldLocation> WaterTiles(int seed)
            {
                var (world, ctx) = BuildOutdoorWorld(40, 40, seed);
                new RiverCarverFeature(width: 3, connectEdges: true).Apply(world, ctx);
                var water = new List<WorldLocation>();
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++)
                    {
                        var loc = new WorldLocation(x, y, 0);
                        if (world.GetTerrainType(loc)?.Name == "Water")
                            water.Add(loc);
                    }
                return water;
            }

            var run1 = WaterTiles(999);
            var run2 = WaterTiles(999);

            Assert.That(run1.Count, Is.EqualTo(run2.Count));
            for (int i = 0; i < run1.Count; i++)
                Assert.That(run1[i], Is.EqualTo(run2[i]));
        }
    }
}
