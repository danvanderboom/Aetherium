using System;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldBuilders;
using Aetherium.Lighting;
using Aetherium.Entities;
using Aetherium.Systems;

namespace Aetherium.Test
{
    public class LightingPropagationTests
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
        public void LightSource_EmitsAtOrigin()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));

            var lightEntity = new Aetherium.Entities.LightEntity();
            lightEntity.Set(new LightSource(1.0, 5));
            lightEntity.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(lightEntity);

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(-2, -2, 5, 5);

            calculator.ComputeLightFromSource(world, new WorldLocation(0, 0, 0), 1.0, 5, bounds, frame);

            var originLight = frame.GetLightLevel(new WorldLocation(0, 0, 0));
            Assert.Greater(originLight, 0.0, "Light source origin should have light");
        }

        [Test]
        public void Light_PropagatesInOpenSpace()
        {
            var world = CreateWorldWithTiles();
            // Create open corridor
            for (int x = 0; x < 5; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 6, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            // Cells along the corridor should have light
            Assert.Greater(frame.GetLightLevel(new WorldLocation(1, 0, 0)), 0.0);
            Assert.Greater(frame.GetLightLevel(new WorldLocation(2, 0, 0)), 0.0);
            Assert.Greater(frame.GetLightLevel(new WorldLocation(3, 0, 0)), 0.0);
        }

        [Test]
        public void Light_AttenuatesWithDistance()
        {
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 10; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 11, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            var closeLight = frame.GetLightLevel(new WorldLocation(1, 0, 0));
            var farLight = frame.GetLightLevel(new WorldLocation(8, 0, 0));

            Assert.Greater(closeLight, farLight, "Light closer to source should be brighter");
        }

        [Test]
        public void Wall_BlocksLight()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Wall", new WorldLocation(1, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(2, 0, 0));

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 4, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            // Light should not pass through wall
            var beyondWall = frame.GetLightLevel(new WorldLocation(2, 0, 0));
            Assert.AreEqual(0.0, beyondWall, 0.001, "Wall should block light completely");
        }

        [Test]
        public void ClosedDoor_BlocksLight()
        {
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 5; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var door = new Door();
            door.Set(new WorldLocation(2, 0, 0));
            world.AddEntity(door);

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 6, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            var beyondDoor = frame.GetLightLevel(new WorldLocation(3, 0, 0));
            Assert.AreEqual(0.0, beyondDoor, 0.001, "Closed door should block light");
        }

        [Test]
        public void OpenDoor_AllowsLight()
        {
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 5; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var door = new Door();
            door.Set(new WorldLocation(2, 0, 0));
            door.Get<OpensAndCloses>().IsOpen = true;
            world.AddEntity(door);

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 6, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            var beyondDoor = frame.GetLightLevel(new WorldLocation(3, 0, 0));
            Assert.Greater(beyondDoor, 0.0, "Open door should allow light to pass");
        }

        [Test]
        public void Forest_PartialOpacity_ReducesLight()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Forest", new WorldLocation(1, 0, 0));
            world.SetTerrain("Forest", new WorldLocation(2, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(3, 0, 0));

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 5, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 10, bounds, frame);

            // Light should pass through forest but be reduced
            var throughForest = frame.GetLightLevel(new WorldLocation(3, 0, 0));
            Assert.Less(throughForest, 1.0, "Forest should reduce light intensity");
            // But not completely blocked if opacity < 1.0
            Assert.Greater(throughForest, 0.0, "Partial opacity should allow some light");
        }

        [Test]
        public void Light_RespectsMaxRange()
        {
            var world = CreateWorldWithTiles();
            for (int x = 0; x < 15; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var calculator = new LightCalculator();
            var frame = new LightFrame();
            var bounds = new Rectangle(0, -1, 16, 3);
            var source = new WorldLocation(0, 0, 0);

            calculator.ComputeLightFromSource(world, source, 1.0, 5, bounds, frame);

            // Light should not reach beyond range
            var beyondRange = frame.GetLightLevel(new WorldLocation(10, 0, 0));
            Assert.AreEqual(0.0, beyondRange, "Light should not reach beyond max range");
        }

        [Test]
        public void MultipleLightSources_AccumulateLight()
        {
            var world = CreateWorldWithTiles();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(5, 0, 0));

            var light1 = new Aetherium.Entities.LightEntity();
            light1.Set(new LightSource(0.5, 10));
            light1.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(light1);

            var light2 = new Aetherium.Entities.LightEntity();
            light2.Set(new LightSource(0.5, 10));
            light2.Set(new WorldLocation(5, 0, 0));
            world.AddEntity(light2);

            var system = new LightingSystem();
            var bounds = new Rectangle(0, -1, 6, 3);
            var frame = system.ComputeLighting(world, bounds, 0);

            // Middle point (2.5, 0) should receive light from both sources
            var middlePoint = new WorldLocation(2, 0, 0);
            var lightLevel = frame.GetLightLevel(middlePoint);
            Assert.Greater(lightLevel, 0.0, "Multiple sources should accumulate light");
        }
    }
}


