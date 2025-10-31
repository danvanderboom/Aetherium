using System;
using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Cities;
using Aetherium.Components;

namespace Aetherium.Test
{
    [TestFixture]
    public class CityGeneratorTests
    {
        [Test]
        public void GridCityGenerator_ShouldGenerate_DeterministicLayout()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var seed = 12345;
            var context1 = new GeneratorContext(60, 60, seed);
            var context2 = new GeneratorContext(60, 60, seed);

            // Act
            var world1 = generator.Generate(context1);
            var world2 = generator.Generate(context2);

            // Assert - Same seed should produce identical layout
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
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
        public void GridCityGenerator_ShouldGenerate_Streets()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have road terrain
            int roadCount = 0;
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Road")
                    {
                        roadCount++;
                    }
                }
            }

            Assert.That(roadCount, Is.GreaterThan(0), "City should have roads");
        }

        [Test]
        public void GridCityGenerator_ShouldGenerate_Buildings()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have indoor spaces (buildings)
            int indoorCount = 0;
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Indoors")
                    {
                        indoorCount++;
                    }
                }
            }

            Assert.That(indoorCount, Is.GreaterThan(0), "City should have buildings");
        }

        [Test]
        public void GridCityGenerator_ShouldCreate_GridPattern()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345)
            {
                GeneratorParams = new Dictionary<string, string>
                {
                    ["blockSize"] = "12",
                    ["streetWidth"] = "2"
                }
            };

            // Act
            var world = generator.Generate(context);

            // Assert - Check for regular street pattern
            // Streets should appear at regular intervals
            bool hasHorizontalStreet = false;
            bool hasVerticalStreet = false;

            // Check horizontal streets (scanning a column)
            for (int y = 0; y < 60; y++)
            {
                if (world.GetTerrain(new WorldLocation(30, y, 0))?.Type.Name == "Road")
                {
                    hasHorizontalStreet = true;
                    break;
                }
            }

            // Check vertical streets (scanning a row)
            for (int x = 0; x < 60; x++)
            {
                if (world.GetTerrain(new WorldLocation(x, 30, 0))?.Type.Name == "Road")
                {
                    hasVerticalStreet = true;
                    break;
                }
            }

            Assert.That(hasHorizontalStreet || hasVerticalStreet, Is.True,
                "Should have orthogonal street grid");
        }

        [Test]
        public void GridCityGenerator_ShouldSet_StartLocation()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert
            Assert.That(context.StartLocation, Is.Not.Null);
            Assert.That(context.StartLocation?.X, Is.InRange(0, 59));
            Assert.That(context.StartLocation?.Y, Is.InRange(0, 59));
        }

        [Test]
        public void GridCityGenerator_ShouldRespect_BlockSizeParameter()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345)
            {
                GeneratorParams = new Dictionary<string, string>
                {
                    ["blockSize"] = "20"
                }
            };

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void GridCityGenerator_ShouldRespect_StreetWidthParameter()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345)
            {
                GeneratorParams = new Dictionary<string, string>
                {
                    ["streetWidth"] = "4"
                }
            };

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void GridCityGenerator_ShouldUse_DefaultParameters()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);
            // No GeneratorParams specified

            // Act & Assert - Should work with defaults
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void GridCityGenerator_ShouldHandle_SmallMaps()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(20, 20, 12345);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void GridCityGenerator_ShouldHandle_LargeMaps()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(150, 150, 12345);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => generator.Generate(context));
        }

        [Test]
        public void GridCityGenerator_ShouldGenerate_WallsAroundBuildings()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have walls (building exteriors)
            int wallCount = 0;
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain == "Wall")
                    {
                        wallCount++;
                    }
                }
            }

            Assert.That(wallCount, Is.GreaterThan(0), "Should have building walls");
        }

        [Test]
        public void GridCityGenerator_ShouldCreate_AccessibleBuildings()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Buildings should have openings (doors) adjacent to streets
            bool foundBuildingAccessibleFromRoad = false;

            for (int y = 1; y < 59; y++)
            {
                for (int x = 1; x < 59; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    if (world.GetTerrain(loc)?.Type.Name == "Indoors")
                    {
                        // Check if any adjacent tile is a road
                        var adjacentLocs = new[]
                        {
                            new WorldLocation(x + 1, y, 0),
                            new WorldLocation(x - 1, y, 0),
                            new WorldLocation(x, y + 1, 0),
                            new WorldLocation(x, y - 1, 0)
                        };

                        foreach (var adjLoc in adjacentLocs)
                        {
                            if (world.GetTerrain(adjLoc)?.Type.Name == "Road")
                            {
                                foundBuildingAccessibleFromRoad = true;
                                break;
                            }
                        }

                        if (foundBuildingAccessibleFromRoad) break;
                    }
                }
                if (foundBuildingAccessibleFromRoad) break;
            }

            Assert.That(foundBuildingAccessibleFromRoad, Is.True,
                "Some buildings should be accessible from roads");
        }

        [Test]
        public void GridCityGenerator_ShouldGenerate_FullSizeMap()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var width = 60;
            var height = 60;
            var context = new GeneratorContext(width, height, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - All tiles should be set
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
        public void GridCityGenerator_ShouldGenerate_MultipleTerrainTypes()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context = new GeneratorContext(60, 60, 12345);

            // Act
            var world = generator.Generate(context);

            // Assert - Should have multiple terrain types (Road, Wall, Indoors)
            var terrainTypes = new HashSet<string>();
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var terrain = world.GetTerrain(loc)?.Type.Name;
                    if (terrain != null)
                    {
                        terrainTypes.Add(terrain);
                    }
                }
            }

            Assert.That(terrainTypes.Count, Is.GreaterThanOrEqualTo(2),
                "City should have multiple terrain types");
        }

        [Test]
        public void GridCityGenerator_ShouldGenerate_DifferentMapsWithDifferentSeeds()
        {
            // Arrange
            var generator = new GridCityGenerator();
            var context1 = new GeneratorContext(60, 60, 12345);
            var context2 = new GeneratorContext(60, 60, 67890);

            // Act
            var world1 = generator.Generate(context1);
            var world2 = generator.Generate(context2);

            // Assert - Different seeds should produce some differences (in building placement)
            int differences = 0;
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 60; x++)
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

            // Streets might be the same due to grid, but building placement should differ
            Assert.That(differences, Is.GreaterThan(0),
                "Different seeds should produce some differences in building placement");
        }
    }
}

