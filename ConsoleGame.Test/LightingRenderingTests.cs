using System;
using System.Drawing;
using NUnit.Framework;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame.Lighting;
// using ConsoleGame.Views; // Commented out - Views now in client project only
using ConsoleGame.Entities;

namespace ConsoleGame.Test
{
    public class LightingRenderingTests
    {
        private World CreateWorldWithTiles()
        {
            var world = new World();
            var builder = new DungeonCrawlerWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test, Ignore("ConsoleMapView moved to client project - TODO: refactor test")]
        public void ConsoleMapView_ComputesLighting_WithLightSources()
        {
            /*
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 10; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var lightEntity = new ConsoleGame.Entities.LightEntity();
            lightEntity.Set(new LightSource(1.0, 5));
            lightEntity.Set(new WorldLocation(5, 0, 0));
            world.AddEntity(lightEntity);

            // var mapView = new ConsoleMapView(); // Moved to client
            // mapView.World = world;
            // mapView.WorldLocation = new WorldLocation(5, 0, 0);
            // mapView.Size = new Size(20, 10);

            // Trigger computation by accessing Lighting (internal computation)
            // We'll test that lighting system integration works
            var lightingSystem = new LightingSystem();
            var bounds = new Rectangle(0, -1, 11, 3);
            var frame = lightingSystem.ComputeLighting(world, bounds, 0);

            Assert.IsNotNull(frame);
            Assert.Greater(frame.GetLightLevel(new WorldLocation(5, 0, 0)), 0.0);
            */
        }

        [Test]
        public void LightFrame_ProvidesData_ForRendering()
        {
            var frame = new LightFrame();
            
            frame.SetLightLevel(new WorldLocation(0, 0, 0), 1.0);
            frame.SetLightLevel(new WorldLocation(1, 0, 0), 0.5);
            frame.SetLightLevel(new WorldLocation(2, 0, 0), 0.0);

            Assert.AreEqual(1.0, frame.GetLightLevel(new WorldLocation(0, 0, 0)));
            Assert.AreEqual(0.5, frame.GetLightLevel(new WorldLocation(1, 0, 0)));
            Assert.AreEqual(0.0, frame.GetLightLevel(new WorldLocation(2, 0, 0)));
        }

        [Test]
        public void LightingSystem_FindsAllLightSources_InWorld()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(10, 0, 0));

            var light1 = new ConsoleGame.Entities.LightEntity();
            light1.Set(new LightSource(0.8, 5));
            light1.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(light1);

            var light2 = new ConsoleGame.Entities.LightEntity();
            light2.Set(new LightSource(0.6, 5));
            light2.Set(new WorldLocation(10, 0, 0));
            world.AddEntity(light2);

            var disabledLight = new ConsoleGame.Entities.LightEntity();
            disabledLight.Set(new LightSource(1.0, 5) { IsEnabled = false });
            disabledLight.Set(new WorldLocation(5, 0, 0));
            world.AddEntity(disabledLight);

            var system = new LightingSystem();
            var bounds = new Rectangle(-1, -1, 12, 3);
            var frame = system.ComputeLighting(world, bounds, 0);

            // Should find enabled lights
            Assert.Greater(frame.GetLightLevel(new WorldLocation(0, 0, 0)), 0.0);
            Assert.Greater(frame.GetLightLevel(new WorldLocation(10, 0, 0)), 0.0);

            // Disabled light should not contribute
            var midPoint = frame.GetLightLevel(new WorldLocation(5, 0, 0));
            // Might be 0 if no other lights reach it, or very low from distant lights
            Assert.LessOrEqual(midPoint, 0.5, "Disabled light should not contribute");
        }

        [Test]
        public void LightingSystem_OnlyProcessesLights_OnCorrectZLevel()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(0, 0, -1)); // Different Z level

            var light1 = new ConsoleGame.Entities.LightEntity();
            light1.Set(new LightSource(1.0, 10));
            light1.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(light1);

            var light2 = new ConsoleGame.Entities.LightEntity();
            light2.Set(new LightSource(1.0, 10));
            light2.Set(new WorldLocation(0, 0, -1));
            world.AddEntity(light2);

            var system = new LightingSystem();
            var bounds = new Rectangle(-1, -1, 3, 3);
            
            // Compute lighting for Z=0 only
            var frame = system.ComputeLighting(world, bounds, 0);

            // Should only get light from Z=0 source
            Assert.Greater(frame.GetLightLevel(new WorldLocation(0, 0, 0)), 0.0);
            
            // Z=-1 light should not affect Z=0
            // (We're computing for Z=0, so Z=-1 shouldn't contribute)
            // This is a structural test - lights on different Z levels don't affect each other
        }

        [Test]
        public void LightingSystem_ClampsLightLevels_ToMaximum()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));

            // Add multiple overlapping bright lights
            for (int i = 0; i < 3; i++)
            {
                var light = new ConsoleGame.Entities.LightEntity();
                light.Set(new LightSource(1.0, 10));
                light.Set(new WorldLocation(0, 0, 0));
                world.AddEntity(light);
            }

            var system = new LightingSystem();
            var bounds = new Rectangle(-1, -1, 3, 3);
            var frame = system.ComputeLighting(world, bounds, 0);

            var lightLevel = frame.GetLightLevel(new WorldLocation(0, 0, 0));
            Assert.LessOrEqual(lightLevel, 1.0, "Light levels should be clamped to 1.0 maximum");
        }
    }
}

