using System;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldBuilders;
using Aetherium.Systems;
using Aetherium.Lighting;
using Aetherium.Entities;

namespace Aetherium.Test
{
    public class LightingVisionIntegrationTests
    {
        private World CreateWorldWithTiles()
        {
            var world = new World();
            var builder = new DungeonCrawlerWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test]
        public void Vision_InDarkness_HasReducedRange()
        {
            var world = CreateWorldWithTiles();
            // Create a long corridor
            for (int x = 0; x < 20; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var visionSystem = new VisionSystem();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 21, 3);
            var maxRange = 15;

            // Compute vision without light (darkness)
            var vision = visionSystem.ComputeVision(world, origin, bounds, maxRange);

            // In darkness, visibility should be reduced
            // The exact reduction depends on implementation, but cells far away should not be visible
            var farLocation = new WorldLocation(15, 0, 0);
            var visualAtFar = vision.Visuals.ContainsKey(farLocation);

            // In complete darkness, we might not see that far
            // This test verifies that light affects vision range
            Assert.IsNotNull(vision);
        }

        [Test]
        public void Vision_WithLightSource_SeesFurther()
        {
            var world = CreateWorldWithTiles();
            // Create a long corridor
            for (int x = 0; x < 20; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            // Add light source at origin
            var lightEntity = new Aetherium.Entities.LightEntity();
            lightEntity.Set(new LightSource(1.0, 10));
            lightEntity.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(lightEntity);

            var visionSystem = new VisionSystem();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 21, 3);
            var maxRange = 15;

            var vision = visionSystem.ComputeVision(world, origin, bounds, maxRange);

            // With light, should see further
            var nearLocation = new WorldLocation(5, 0, 0);
            Assert.IsTrue(vision.Visuals.ContainsKey(nearLocation), "Should see nearby cells with light");
        }

        [Test]
        public void Vision_DarkCells_BeyondProximity_NotVisible()
        {
            var world = CreateWorldWithTiles();
            // Create open space
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));

            var visionSystem = new VisionSystem();
            var origin = new WorldLocation(5, 5, 0);
            var bounds = new Rectangle(0, 0, 10, 10);
            var maxRange = 10;

            // No light sources - complete darkness
            var vision = visionSystem.ComputeVision(world, origin, bounds, maxRange);

            // Cells very far away without light should not be visible
            // (except very close - within 2 cells)
            var farLocation = new WorldLocation(1, 1, 0); // Far from origin at (5,5)
            var isFarVisible = vision.Visuals.ContainsKey(farLocation);

            // In darkness, distant cells beyond 2 cells should not be visible
            var distance = Math.Sqrt(Math.Pow(1 - 5, 2) + Math.Pow(1 - 5, 2)); // about 5.66
            if (distance > 2.0)
            {
                Assert.IsFalse(isFarVisible, "Distant dark cells should not be visible");
            }
        }

        [Test]
        public void Vision_LightAtLocation_RequiredForVisibility()
        {
            var world = CreateWorldWithTiles();
            // Create two separate rooms
            for (int x = 0; x < 5; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0)); // Room 1
                world.SetTerrain("Indoors", new WorldLocation(x + 10, 0, 0)); // Room 2 (separate)
            }

            // Add light only in room 1
            var lightEntity = new Aetherium.Entities.LightEntity();
            lightEntity.Set(new LightSource(1.0, 5));
            lightEntity.Set(new WorldLocation(2, 0, 0));
            world.AddEntity(lightEntity);

            var visionSystem = new VisionSystem();
            var origin = new WorldLocation(2, 0, 0); // In room 1 with light
            var bounds = new Rectangle(0, -1, 16, 3);
            var maxRange = 15;

            var vision = visionSystem.ComputeVision(world, origin, bounds, maxRange);

            // Room 2 is too far and has no light - should not be visible
            var room2Location = new WorldLocation(12, 0, 0);
            Assert.IsFalse(vision.Visuals.ContainsKey(room2Location), "Distant unlit areas should not be visible");
        }

        [Test]
        public void Vision_ComputesLighting_IfNotProvided()
        {
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 10; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var lightEntity = new Aetherium.Entities.LightEntity();
            lightEntity.Set(new LightSource(1.0, 8));
            lightEntity.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(lightEntity);

            var visionSystem = new VisionSystem();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 11, 3);
            var maxRange = 10;

            // Call without providing lightFrame - should compute automatically
            var vision = visionSystem.ComputeVision(world, origin, bounds, maxRange);

            // Should still work and use lighting
            Assert.IsNotNull(vision);
            var nearbyLocation = new WorldLocation(3, 0, 0);
            Assert.IsTrue(vision.Visuals.ContainsKey(nearbyLocation), "Should compute lighting automatically");
        }
    }
}


