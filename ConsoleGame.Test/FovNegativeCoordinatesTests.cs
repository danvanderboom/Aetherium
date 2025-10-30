using System;
using System.Drawing;
using NUnit.Framework;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Systems;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame.Test
{
    /// <summary>
    /// Tests for FOV with negative coordinates (like leaving the room to the left).
    /// </summary>
    [TestFixture]
    public class FovNegativeCoordinatesTests
    {
        private World BuildTestWorld()
        {
            var builder = new TestMazeWorldBuilder();
            return builder.Build();
        }

        [Test]
        public void FOV_With_Negative_Bounds_X()
        {
            // Simulate the scenario from the screenshot: bounds with negative X
            var world = BuildTestWorld();
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(-3, 15, 0);
            var bounds = new Rectangle(-13, 5, 20, 20); // X: -13 to 7, Y: 5 to 25
            
            // Verify origin is in bounds
            Assert.True(bounds.Contains(new Point(origin.X, origin.Y)), "Origin should be in bounds");
            
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);
            
            // Origin should always be visible
            var originArrayY = origin.Y - bounds.Y;
            var originArrayX = origin.X - bounds.X;
            Assert.True(visible[originArrayY, originArrayX], 
                $"Origin ({origin.X},{origin.Y}) should be visible at array index [{originArrayY},{originArrayX}]");
            
            // Check a few cells around origin should be visible (if terrain allows)
            // At (-3,15), adjacent cells should be visible if not blocked
            Assert.True(visible[15 - bounds.Y, -2 - bounds.X] || visible[15 - bounds.Y, -4 - bounds.X] || 
                       visible[16 - bounds.Y, -3 - bounds.X] || visible[14 - bounds.Y, -3 - bounds.X],
                "At least one adjacent cell to origin should be visible");
        }

        [Test]
        public void FOV_Array_Index_Calculation_With_Negative_Bounds()
        {
            // Test that array index calculation works correctly with negative bounds
            var world = BuildTestWorld();
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(-3, 15, 0);
            var bounds = new Rectangle(-13, 5, 20, 20);
            
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 20);
            
            // Verify array indexing: visible[y - bounds.Y, x - bounds.X]
            // For origin (-3, 15) in bounds (-13, 5):
            // Array Y index = 15 - 5 = 10
            // Array X index = -3 - (-13) = 10
            var originArrayY = 15 - 5;  // 10
            var originArrayX = -3 - (-13); // 10
            Assert.True(originArrayY >= 0 && originArrayY < 20, "Origin Y index should be in range");
            Assert.True(originArrayX >= 0 && originArrayX < 20, "Origin X index should be in range");
            Assert.True(visible[originArrayY, originArrayX], "Origin should be visible");
        }

        [Test]
        public void VisionFrame_Contains_WorldLocations_Correctly()
        {
            // Test that VisionFrame properly stores locations with negative coordinates
            var world = BuildTestWorld();
            var visionSystem = new VisionSystem();
            
            var origin = new WorldLocation(-3, 15, 0);
            var bounds = new Rectangle(-13, 5, 20, 20);
            var maxRange = 20;
            
            var frame = visionSystem.ComputeVision(world, origin, bounds, maxRange);
            
            // The frame should contain the origin location
            Assert.True(frame.Visuals.ContainsKey(origin), "VisionFrame should contain origin location");
            
            // Check that it contains some visible locations
            Assert.Greater(frame.Visuals.Count, 0, "VisionFrame should contain at least the origin");
        }
    }
}

