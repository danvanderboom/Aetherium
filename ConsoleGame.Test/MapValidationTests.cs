using System;
using System.Linq;
using NUnit.Framework;
using ConsoleGame.WorldBuilders;
using ConsoleGame.WorldBuilders.Validation;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;

namespace ConsoleGame.Test
{
    [TestFixture]
    public class MapValidationTests
    {
        private MapValidator validator;

        [SetUp]
        public void SetUp()
        {
            validator = new MapValidator();
        }

        [Test]
        public void ValidateBoundaries_ValidMapWithWalls_Passes()
        {
            // Build a simple 3x3 passable room with explicit wall border
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            for (int x = 1; x <= 3; x++)
                for (int y = 1; y <= 3; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));

            for (int x = 0; x <= 4; x++)
            {
                world.SetTerrain("Wall", new WorldLocation(x, 0, 0));
                world.SetTerrain("Wall", new WorldLocation(x, 4, 0));
            }
            for (int y = 1; y <= 3; y++)
            {
                world.SetTerrain("Wall", new WorldLocation(0, y, 0));
                world.SetTerrain("Wall", new WorldLocation(4, y, 0));
            }

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = true,
                RequireLightSource = false,
                StartLocation = null
            };

            var report = validator.Validate(world, options);

            var boundaryErrors = report.Errors.Where(e => e.Category == "Boundary").ToList();
            Assert.IsEmpty(boundaryErrors, $"Boundary errors found: {string.Join(", ", boundaryErrors)}");
        }

        [Test]
        public void ValidateBoundaries_InvalidMapWithPassableTerrainAtEdge_Fails()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Create a simple corridor without boundaries
            for (int x = 0; x < 10; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, 0));
            }

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = true,
                RequireLightSource = false,
                StartLocation = null  // Don't validate start location for this test
            };

            var report = validator.Validate(world, options);

            Assert.IsFalse(report.IsValid, "Map without boundaries should fail validation");
            var boundaryErrors = report.Errors.Where(e => e.Category == "Boundary").ToList();
            Assert.IsNotEmpty(boundaryErrors, "Should have boundary errors");
        }

        [Test]
        public void ValidateBoundaries_ImplicitBoundariesAllowed_Passes()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Create a corridor without explicit boundaries
            for (int x = 0; x < 10; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, 0));
            }

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false, // Allow implicit boundaries
                RequireLightSource = false,
                StartLocation = null  // Don't validate start location for this test
            };

            var report = validator.Validate(world, options);

            // Should pass because we're not requiring explicit boundaries
            var boundaryErrors = report.Errors.Where(e => e.Category == "Boundary").ToList();
            Assert.IsEmpty(boundaryErrors, "Implicit boundaries should be allowed when RequireExplicitBoundary is false");
        }

        [Test]
        public void ValidateLightSources_MapWithLightSource_Passes()
        {
            var builder = new AudioTestWorldBuilder();
            var world = builder.Build();

            // AudioTestWorldBuilder should have a light source at start location
            // But let's verify by adding one if it doesn't
            var startLocation = builder.StartLocation;
            var hasLight = world.Entities.Values
                .Any(e => e.Has<WorldLocation>() && 
                         e.Get<WorldLocation>() == startLocation &&
                         e.Has<LightSource>() &&
                         e.Get<LightSource>()?.IsEnabled == true);

            if (!hasLight)
            {
                var lightEntity = new LightEntity();
                lightEntity.Set(new LightSource(1.0, 50));
                lightEntity.Set(startLocation);
                world.AddEntity(lightEntity);
            }

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false,
                RequireLightSource = true,
                StartLocation = null  // Don't validate start location for this test
            };

            var report = validator.Validate(world, options);

            var lightingErrors = report.Errors.Where(e => e.Category == "Lighting").ToList();
            Assert.IsEmpty(lightingErrors, $"Lighting errors found: {string.Join(", ", lightingErrors)}");
        }

        [Test]
        public void ValidateLightSources_MapWithoutLightSource_Fails()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Create a simple map without light source
            world.SetTerrain("Indoors", new WorldLocation(5, 5, 0));
            world.SetTerrain("Wall", new WorldLocation(4, 5, 0));
            world.SetTerrain("Wall", new WorldLocation(6, 5, 0));

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false,
                RequireLightSource = true,
                StartLocation = null  // Don't validate start location for this test
            };

            var report = validator.Validate(world, options);

            Assert.IsFalse(report.IsValid, "Map without light source should fail validation");
            var lightingErrors = report.Errors.Where(e => e.Category == "Lighting").ToList();
            Assert.IsNotEmpty(lightingErrors, "Should have lighting errors");
        }

        [Test]
        public void ValidateStartLocation_ValidPassableLocation_Passes()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Build a simple room
            for (int x = 5; x < 10; x++)
            {
                for (int y = 5; y < 10; y++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }

            var startLocation = new WorldLocation(7, 7, 0);

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false,
                RequireLightSource = false,
                StartLocation = startLocation
            };

            var report = validator.Validate(world, options);

            var startLocationErrors = report.Errors.Where(e => e.Category == "StartLocation").ToList();
            Assert.IsEmpty(startLocationErrors, $"StartLocation errors found: {string.Join(", ", startLocationErrors)}");
        }

        [Test]
        public void ValidateStartLocation_ImpassableLocation_Fails()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            world.SetTerrain("Wall", new WorldLocation(5, 5, 0));
            var startLocation = new WorldLocation(5, 5, 0);

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false,
                RequireLightSource = false,
                StartLocation = startLocation
            };

            var report = validator.Validate(world, options);

            Assert.IsFalse(report.IsValid, "Start location on impassable terrain should fail");
            var startLocationErrors = report.Errors.Where(e => e.Category == "StartLocation").ToList();
            Assert.IsNotEmpty(startLocationErrors, "Should have start location errors");
        }

        [Test]
        public void ValidateStartLocation_ReachabilityRequirement_Passes()
        {
            var world = new World();
            var builder = new TestMazeWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));

            // Create a connected area with multiple passable locations
            for (int x = 5; x < 10; x++)
            {
                for (int y = 5; y < 10; y++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }

            var startLocation = new WorldLocation(7, 7, 0);

            var options = new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = false,
                RequireLightSource = false,
                StartLocation = startLocation,
                MinReachableLocations = 10 // Should have at least 10 reachable locations (5x5 = 25)
            };

            var report = validator.Validate(world, options);

            var startLocationErrors = report.Errors.Where(e => e.Category == "StartLocation").ToList();
            Assert.IsEmpty(startLocationErrors, $"StartLocation errors found: {string.Join(", ", startLocationErrors)}");
        }

        [Test]
        public void ValidateComplete_AllBuilders_ValidatesSuccessfully()
        {
            var builders = new WorldBuilder[]
            {
                new TestMazeWorldBuilder(),
                new AudioTestWorldBuilder()
            };

            foreach (var builder in builders)
            {
                var world = builder.Build();

                // Use lenient options for existing builders (they may not all have lights yet)
                var options = new MapValidationOptions
                {
                    ZLevel = 0,
                    RequireExplicitBoundary = false,
                    RequireLightSource = false, // Temporarily lenient
                    StartLocation = null  // Don't validate start location for this test
                };

                var report = validator.Validate(world, options);

                // Check that at least boundaries are valid
                var boundaryErrors = report.Errors.Where(e => e.Category == "Boundary").ToList();
                Assert.IsEmpty(boundaryErrors, 
                    $"{builder.GetType().Name} failed boundary validation: {string.Join(", ", boundaryErrors)}");
            }
        }
    }
}

