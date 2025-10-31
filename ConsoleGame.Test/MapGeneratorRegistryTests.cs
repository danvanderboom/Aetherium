using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ConsoleGame.WorldGen;

namespace ConsoleGame.Test
{
    [TestFixture]
    public class MapGeneratorRegistryTests
    {
        [Test]
        public void MapGeneratorRegistry_ShouldDiscover_Generators()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;

            // Act
            registry.DiscoverTypes(assembly);
            var generators = registry.ListGenerators().ToList();

            // Assert
            Assert.That(generators.Count, Is.GreaterThan(0));
            Assert.That(generators, Contains.Item("Maze"));
        }

        [Test]
        public void MapGeneratorRegistry_ShouldDiscover_Features()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;

            // Act
            registry.DiscoverTypes(assembly);
            var features = registry.ListFeatures().ToList();

            // Assert
            Assert.That(features.Count, Is.GreaterThan(0));
        }

        [Test]
        public void MapGeneratorRegistry_ShouldInstantiate_MazeGenerator()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generator = registry.GetGenerator("Maze");

            // Assert
            Assert.That(generator, Is.Not.Null);
            Assert.That(generator, Is.InstanceOf<MazeGenerator>());
        }

        [Test]
        public void MapGeneratorRegistry_ShouldReturn_NullForInvalidGenerator()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generator = registry.GetGenerator("NonexistentGenerator");

            // Assert
            Assert.That(generator, Is.Null);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldReturn_NullForInvalidFeature()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var feature = registry.GetFeature("NonexistentFeature");

            // Assert
            Assert.That(feature, Is.Null);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldDiscover_FromCurrentAssembly()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();

            // Act - No assemblies specified should use current assembly
            registry.DiscoverTypes();
            var generators = registry.ListGenerators().ToList();

            // Assert - Should have at least some types discovered
            Assert.That(generators, Is.Not.Null);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldRemove_GeneratorSuffix()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();

            // Assert - Names should not include "Generator" suffix
            Assert.That(generators.Any(g => g.EndsWith("Generator")), Is.False);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldDiscover_OutdoorTerrainGenerator()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();
            var generator = registry.GetGenerator("OutdoorTerrain");

            // Assert
            Assert.That(generators, Contains.Item("OutdoorTerrain"));
            Assert.That(generator, Is.Not.Null);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldDiscover_CityGenerator()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();
            var generator = registry.GetGenerator("City");

            // Assert
            Assert.That(generators, Contains.Item("City"));
            Assert.That(generator, Is.Not.Null);
        }

        [Test]
        public void MapGeneratorRegistry_ShouldList_AllGenerators()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();

            // Assert
            Assert.That(generators.Count, Is.GreaterThanOrEqualTo(3)); // At least Maze, OutdoorTerrain, City
            Console.WriteLine($"Discovered generators: {string.Join(", ", generators)}");
        }

        [Test]
        public void MapGeneratorRegistry_ShouldList_AllFeatures()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var features = registry.ListFeatures().ToList();

            // Assert
            Assert.That(features, Is.Not.Null);
            Console.WriteLine($"Discovered features: {string.Join(", ", features)}");
        }

        [Test]
        public void MapGeneratorRegistry_ShouldHandle_MultipleAssemblies()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly1 = typeof(MazeGenerator).Assembly;
            var assembly2 = Assembly.GetExecutingAssembly();

            // Act
            registry.DiscoverTypes(assembly1, assembly2);
            var generators = registry.ListGenerators().ToList();

            // Assert
            Assert.That(generators.Count, Is.GreaterThan(0));
        }

        [Test]
        public void MapGeneratorRegistry_ShouldNot_DiscoverInterfaces()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(IMapGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();

            // Assert - Should not include "IMapGenerator" or "IGenerationFeature"
            Assert.That(generators, Does.Not.Contain("IMapGenerator"));
            Assert.That(generators, Does.Not.Contain("IGenerationFeature"));
        }

        [Test]
        public void MapGeneratorRegistry_ShouldNot_DiscoverAbstractClasses()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var generators = registry.ListGenerators().ToList();
            var features = registry.ListFeatures().ToList();

            // Assert - All should be instantiable
            foreach (var name in generators)
            {
                var gen = registry.GetGenerator(name);
                Assert.That(gen, Is.Not.Null, $"Generator {name} should be instantiable");
            }
        }

        [Test]
        public void MapGeneratorRegistry_ShouldHandle_DuplicateDiscovery()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;

            // Act - Discover twice
            registry.DiscoverTypes(assembly);
            var firstCount = registry.ListGenerators().Count();
            registry.DiscoverTypes(assembly);
            var secondCount = registry.ListGenerators().Count();

            // Assert - Count should be the same (no duplicates)
            Assert.That(firstCount, Is.EqualTo(secondCount));
        }

        [Test]
        public void MapGeneratorRegistry_ShouldProvide_FreshInstances()
        {
            // Arrange
            var registry = new MapGeneratorRegistry();
            var assembly = typeof(MazeGenerator).Assembly;
            registry.DiscoverTypes(assembly);

            // Act
            var gen1 = registry.GetGenerator("Maze");
            var gen2 = registry.GetGenerator("Maze");

            // Assert - Should be different instances
            Assert.That(gen1, Is.Not.Null);
            Assert.That(gen2, Is.Not.Null);
            Assert.That(gen1, Is.Not.SameAs(gen2));
        }
    }
}

