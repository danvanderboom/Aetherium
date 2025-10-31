using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;
using Aetherium.Components;

namespace Aetherium.Test
{
    [TestFixture]
    public class OutdoorTerrainGeneratorTests
    {
        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_DeterministicMap()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var seed = 12345;
            var context1 = new GeneratorContext(50, 50, seed);
            var context2 = new GeneratorContext(50, 50, seed);

            // Act
            var world1 = generator.Generate(context1);
            var world2 = generator.Generate(context2);

            // Assert - Same seed should produce identical terrain
            for (int y = 0; y < 50; y++)
            {
                for (int x = 0; x < 50; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain1 = world1.GetTerrain(loc)?.Type.Name;
                    var terrain2 = world2.GetTerrain(loc)?.Type.Name;
                    Assert.That(terrain1, Is.EqualTo(terrain2),
                        $"Terrain mismatch at ({x}, {y})");
                }
            }
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_DifferentMapsWithDifferentSeeds()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context1 = new GeneratorContext(50, 50, 12345);
            var context2 = new GeneratorContext(50, 50, 67890);

            // Act
            var world1 = generator.Generate(context1);
            var world2 = generator.Generate(context2);

            // Assert - Different seeds should produce different terrain
            int differences = 0;
            for (int y = 0; y < 50; y++)
            {
                for (int x = 0; x < 50; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain1 = world1.GetTerrain(loc)?.Type.Name;
                    var terrain2 = world2.GetTerrain(loc)?.Type.Name;
                    if (terrain1 != terrain2)
                    {
                        differences++;
                    }
                }
            }

            Assert.That(differences, Is.GreaterThan(100), 
                "Different seeds should produce significantly different terrain");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldSet_StartLocation()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(50, 50, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert
            Assert.That(context.StartLocation, Is.Not.Null);
            Assert.That(context.StartLocation?.X, Is.InRange(0, 49));
            Assert.That(context.StartLocation?.Y, Is.InRange(0, 49));
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_MultipleTerrainTypes()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(100, 100, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have multiple terrain types
            var terrainTypes = new HashSet<string>();
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain != null)
                    {
                        terrainTypes.Add(terrain);
                    }
                }
            }

            Assert.That(terrainTypes.Count, Is.GreaterThan(1),
                "Map should contain multiple terrain types");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldRespect_CustomParameters()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(50, 50, 12345)
            {
                GeneratorParams = new Dictionary<string, string>
                {
                    ["scale"] = "0.1",
                    ["octaves"] = "3",
                    ["waterThreshold"] = "0.2"
                }
            };

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_WaterTerrain()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(100, 100, 12345)
            {
                GeneratorParams = new Dictionary<string, string>
                {
                    ["waterThreshold"] = "0.5" // High water threshold
                }
            };

            // Act
            var world = generator.Generate(context);

            // Assert - Should have some water
            int waterCount = 0;
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Water")
                    {
                        waterCount++;
                    }
                }
            }

            Assert.That(waterCount, Is.GreaterThan(0), "Map should contain water terrain");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_ForestTerrain()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(100, 100, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have some forest
            int forestCount = 0;
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Forest")
                    {
                        forestCount++;
                    }
                }
            }

            Assert.That(forestCount, Is.GreaterThan(0), "Map should contain forest terrain");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_PlainsTerrain()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(100, 100, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have some plains
            int plainsCount = 0;
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Plains")
                    {
                        plainsCount++;
                    }
                }
            }

            Assert.That(plainsCount, Is.GreaterThan(0), "Map should contain plains terrain");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldGenerate_FullSizeMap()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var width = 80;
            var height = 60;
            var context = new GeneratorContext(width, height, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - All tiles should be generated
            int tilesSet = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc);
                    if (terrain != null)
                    {
                        tilesSet++;
                    }
                }
            }

            Assert.That(tilesSet, Is.EqualTo(width * height),
                "All tiles should be generated");
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldHandle_SmallMaps()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(10, 10, 12345);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldHandle_LargeMaps()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(200, 200, 12345);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldUse_DefaultParameters()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(50, 50, 12345);
            // No GeneratorParams specified

            // Act & Assert - Should work with default parameters
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void PerlinTerrainGenerator_ShouldCreate_ContinuousTerrain()
        {
            // Arrange
            var generator = new PerlinTerrainGenerator();
            var context = new GeneratorContext(50, 50, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Adjacent tiles should tend to be similar (continuity check)
            int similarAdjacent = 0;
            int totalAdjacent = 0;

            for (int y = 0; y < 49; y++)
            {
                for (int x = 0; x < 49; x++)
                {
                    var loc1 = new WorldLocation(x, y, 0);
                    var loc2 = new WorldLocation(x + 1, y, 0);
                    var terrain1 = world.GetTerrain(loc1)?.Type.Name;
                    var terrain2 = world.GetTerrain(loc2)?.Type.Name;

                    if (terrain1 == terrain2)
                    {
                        similarAdjacent++;
                    }
                    totalAdjacent++;
                }
            }

            double similarity = (double)similarAdjacent / totalAdjacent;
            Assert.That(similarity, Is.GreaterThan(0.3),
                "Adjacent tiles should show some continuity (Perlin noise should create clusters)");
        }
    }
}

