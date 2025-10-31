using System;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldBuilders;
using Aetherium.Systems;

namespace Aetherium.Test
{
    public class VisionTests
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
        public void ClosedDoor_Blocks_LineOfSight()
        {
            var world = CreateWorldWithTiles();

            // Build a 5x1 indoor corridor at y=0
            for (int x = 0; x < 5; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            // Place a closed door at x=2
            var door = new Aetherium.Entities.Door();
            door.Set(new WorldLocation(2, 0, 0));
            world.AddEntity(door);

            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 6, 3); // cover corridor
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);

            // cell beyond the door (x=3, y=0) should NOT be visible
            Assert.False(visible[0 - bounds.Y, 3 - bounds.X]);
            // the door cell itself should be visible
            Assert.True(visible[0 - bounds.Y, 2 - bounds.X]);
        }

        [Test]
        public void OpenDoor_Allows_LineOfSight()
        {
            var world = CreateWorldWithTiles();

            for (int x = 0; x < 5; x++)
                world.SetTerrain("Indoors", new WorldLocation(x, 0, 0));

            var door = new Aetherium.Entities.Door();
            door.Set(new WorldLocation(2, 0, 0));
            // Open the door
            var oc = door.Get<OpensAndCloses>();
            oc.IsOpen = true;
            world.AddEntity(door);

            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 6, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);

            // cell beyond the door (x=3, y=0) should be visible now
            Assert.True(visible[0 - bounds.Y, 3 - bounds.X]);
        }

        [Test]
        public void Forest_Partial_Opacity_Attenuates_Visibility()
        {
            var world = CreateWorldWithTiles();

            // Create line: origin (x=0) Indoors, then three Forest tiles (x=1..3), then Indoors at x=4
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Forest", new WorldLocation(1, 0, 0));
            world.SetTerrain("Forest", new WorldLocation(2, 0, 0));
            world.SetTerrain("Forest", new WorldLocation(3, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(4, 0, 0));

            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 6, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);

            // Forest opacity defaults to 0.5; cumulative along the ray becomes 0.5, 1.0, 1.5
            // Expect x=1 and x=2 visible; x=3 visible (blocking cell); x=4 not visible
            Assert.True(visible[0 - bounds.Y, 1 - bounds.X]);
            Assert.True(visible[0 - bounds.Y, 2 - bounds.X]);
            Assert.True(visible[0 - bounds.Y, 3 - bounds.X]);
            Assert.False(visible[0 - bounds.Y, 4 - bounds.X]);
        }

        [Test]
        public void Water_Does_Not_Block_Sight()
        {
            var world = CreateWorldWithTiles();

            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Water", new WorldLocation(1, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(2, 0, 0));

            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, -1, 4, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);

            // x=2 should be visible through water
            Assert.True(visible[0 - bounds.Y, 2 - bounds.X]);
        }
    }
}



