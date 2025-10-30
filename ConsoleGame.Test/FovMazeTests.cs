using System;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame.Systems;
using ConsoleGame.Entities;

namespace ConsoleGame.Test
{
    public class FovMazeTests
    {
        private World BuildMazeWorld()
        {
            var builder = new TestMazeWorldBuilder();
            return builder.Build();
        }

        [Test]
        public void Corner_Occlusion_Blocks_LineOfSight()
        {
            var world = BuildMazeWorld();

            // L-corridor: horizontal (5,5) to (10,5), vertical (5,5) to (5,10)
            // Test from (5,5) - should not see (10,10) around the corner
            var fov = new FovCalculator();
            var origin = new WorldLocation(5, 5, 0);
            var bounds = new Rectangle(0, 0, 15, 15);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // (10,10) is around the corner and should not be visible
            Assert.False(visible[10 - bounds.Y, 10 - bounds.X]);
            
            // But (6,5) should be visible (same horizontal corridor)
            Assert.True(visible[5 - bounds.Y, 6 - bounds.X]);
            
            // And (5,6) should be visible (same vertical corridor)
            Assert.True(visible[6 - bounds.Y, 5 - bounds.X]);
        }

        [Test]
        public void Forest_Opacity_Accumulates_In_Corridor()
        {
            var world = BuildMazeWorld();

            // Verify terrain exists at expected locations
            var terrainAt7 = world.GetTerrainType(new WorldLocation(7, 15, 0));
            Assert.NotNull(terrainAt7, "Terrain should exist at (7,15)");
            Assert.AreEqual("Forest", terrainAt7!.Name, "Terrain at (7,15) should be Forest");

            // Forest tiles at (7,15), (8,15), (9,15) in west corridor
            // Test from (5,15) looking east (make sure origin is in clear corridor, just before forest)
            var fov = new FovCalculator();
            var origin = new WorldLocation(5, 15, 0);
            var bounds = new Rectangle(0, 10, 15, 10); // X:0-14, Y:10-19
            
            // Verify origin is in bounds
            Assert.True(bounds.Contains(new Point(origin.X, origin.Y)), "Origin should be in bounds");
            
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // First verify we can see the origin itself
            Assert.True(visible[15 - bounds.Y, 5 - bounds.X], "Origin should be visible");
            
            // Check that we can see adjacent cell first - simplest case
            // For a ray from (5,15) to (6,15), EnumerateLine should yield (6,15)
            // and bounds.Contains(new Point(6,15)) should be true
            var point6_15 = new Point(6, 15);
            Assert.True(bounds.Contains(point6_15), "Point (6,15) should be in bounds");
            
            Assert.True(visible[15 - bounds.Y, 6 - bounds.X], "Adjacent cell (6,15) should be visible");
            
            // After 3 forest tiles (0.49 each = 1.47 cumulative), should block
            // (7,15) and (8,15) may be visible, (9,15) is the blocker, (10,15) should not be
            Assert.True(visible[15 - bounds.Y, 7 - bounds.X], "First forest tile should be visible");
            Assert.True(visible[15 - bounds.Y, 8 - bounds.X], "Second forest tile should be visible");
            Assert.True(visible[15 - bounds.Y, 9 - bounds.X], "Third forest tile (blocker) should be visible");
            Assert.False(visible[15 - bounds.Y, 10 - bounds.X], "Beyond forest should not be visible");
        }

        [Test]
        public void Closed_Door_Blocks_LineOfSight_In_Maze()
        {
            var world = BuildMazeWorld();

            // Door at (25, 15) in east corridor
            var fov = new FovCalculator();
            var origin = new WorldLocation(20, 15, 0); // In central room, looking east
            var bounds = new Rectangle(15, 10, 20, 10);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // (25,15) has the door - it may be visible (the door cell itself)
            // But (26,15) beyond the door should NOT be visible
            Assert.False(visible[15 - bounds.Y, 26 - bounds.X], "Beyond closed door should not be visible");
        }

        [Test]
        public void Open_Door_Allows_LineOfSight_In_Maze()
        {
            var world = BuildMazeWorld();

            // Find and open the door at (25,15)
            Door? door = null;
            if (world.EntitiesByLocation.TryGetValue(new WorldLocation(25, 15, 0), out var entities))
            {
                door = entities.Values.OfType<Door>().FirstOrDefault();
                if (door != null && door.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp))
                {
                    var opens = ocComp as OpensAndCloses;
                    if (opens != null)
                        opens.IsOpen = true;
                }
            }

            Assert.NotNull(door, "Door should exist at (25,15)");

            var fov = new FovCalculator();
            var origin = new WorldLocation(19, 15, 0); // In central room, just west of door
            var bounds = new Rectangle(15, 10, 20, 10); // X:15-34, Y:10-19
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // (26,15) should now be visible through the open door
            Assert.True(visible[15 - bounds.Y, 26 - bounds.X], "Beyond open door should be visible");
        }

        [Test]
        public void Water_Does_Not_Block_Vision_In_Maze()
        {
            var world = BuildMazeWorld();

            // Water tiles at (21,15), (22,15), (23,15) in east corridor
            // Test from (20,15) looking east through water
            // There's a closed door at (25,15) so we won't see past it, but we should see (24,15)
            var fov = new FovCalculator();
            var origin = new WorldLocation(20, 15, 0); // Central room, looking east
            var bounds = new Rectangle(15, 10, 15, 10); // X:15-29, Y:10-19
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // First verify we can see water tiles themselves
            Assert.True(visible[15 - bounds.Y, 21 - bounds.X], "Water tile should be visible");
            Assert.True(visible[15 - bounds.Y, 22 - bounds.X], "Water tile should be visible");
            Assert.True(visible[15 - bounds.Y, 23 - bounds.X], "Water tile should be visible");
            
            // (24,15) beyond the water should be visible (water doesn't block sight)
            Assert.True(visible[15 - bounds.Y, 24 - bounds.X], "Beyond water should be visible (water is transparent)");
            
            // (25,15) has a closed door, should be visible (the door itself)
            Assert.True(visible[15 - bounds.Y, 25 - bounds.X], "Door cell should be visible");
            
            // (26,15) beyond the closed door should NOT be visible
            Assert.False(visible[15 - bounds.Y, 26 - bounds.X], "Beyond closed door should not be visible");
        }

        [Test]
        public void Walls_Block_LineOfSight_Completely()
        {
            var world = BuildMazeWorld();

            // Test from central room (15,15) looking west
            // The west corridor runs from (0,15) to (9,15), then there's a wall at (9,15) 
            // separating the corridor from the central room boundary at (10,15)
            // From (15,15), we should see (14,15) through (10,15) in the central room,
            // then (9,15) through (0,15) in the corridor.
            // But looking northwest from (15,15) to (8,14), there should be walls blocking
            // Actually, let's test from (10,15) looking west - we should see (9,15) (corridor),
            // but (8,15) should also be visible through the corridor.
            // Better test: from (15,15) looking northwest to (8,8) which should be blocked by walls
            var fov = new FovCalculator();
            var origin = new WorldLocation(15, 15, 0);
            var bounds = new Rectangle(5, 5, 15, 15); // X:5-19, Y:5-19
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // From (15,15) looking northwest to (8,8), the line passes through walls
            // (the central room ends at x=9,y=9, so (8,8) is completely outside and separated by walls)
            // This should be blocked - check a cell clearly outside the central room that's not connected
            // (8,8) is northwest and not connected to (15,15) by any corridor - should be blocked by walls
            Assert.False(visible[8 - bounds.Y, 8 - bounds.X], "Through walls should not be visible");
        }

        [Test]
        public void Origin_Always_Visible()
        {
            var world = BuildMazeWorld();

            var fov = new FovCalculator();
            var origin = new WorldLocation(15, 15, 0);
            var bounds = new Rectangle(10, 10, 20, 20);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);

            // Origin (15,15) must always be visible
            Assert.True(visible[15 - bounds.Y, 15 - bounds.X], "Origin must always be visible");
        }

        [Test]
        public void Vision_Respects_MaxRange()
        {
            var world = BuildMazeWorld();

            // Test with a short maxRange in a clear corridor
            // Use west corridor at y=15, from (3,15) looking east
            // Avoid forest tiles at 7,8,9 - test range before forest
            var fov = new FovCalculator();
            var origin = new WorldLocation(3, 15, 0);
            var bounds = new Rectangle(0, 10, 10, 10); // X:0-9, Y:10-19
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 3);

            // Verify origin
            Assert.True(visible[15 - bounds.Y, 3 - bounds.X], "Origin should be visible");
            
            // (6,15) is 3 cells away (distance = sqrt(3^2) = 3.0) - at maxRange, should be visible
            // (7,15) is 4 cells away - beyond maxRange, should not be visible (also happens to be forest)
            Assert.True(visible[15 - bounds.Y, 6 - bounds.X], "At maxRange should be visible");
            Assert.False(visible[15 - bounds.Y, 7 - bounds.X], "Beyond maxRange should not be visible");
        }
    }
}

