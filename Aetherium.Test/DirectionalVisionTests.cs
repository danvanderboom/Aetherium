using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using Aetherium.Core;
using Aetherium.Systems;
using Aetherium.Entities;
using Aetherium.Components;
using World = Aetherium.Core.World;
using TileType = Aetherium.Core.TileType;
using TerrainType = Aetherium.Core.TerrainType;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for directional vision (cone-based FOV filtering).
    /// </summary>
    public class DirectionalVisionTests
    {
        /// <summary>
        /// Creates a simple test world with open terrain.
        /// </summary>
        private World CreateTestWorld()
        {
            var world = new World();
            
            // Create open tile type (doesn't obstruct movement or view)
            var openTileType = new TileType
            {
                Name = "open",
                DefaultComponents = new List<Component>(),
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "." },
                    { "ForegroundColor", ConsoleColor.Gray.ToString() },
                    { "BackgroundColor", ConsoleColor.Black.ToString() },
                }
            };

            // Create terrain type that uses the open tile type
            var openTerrainType = new TerrainType
            {
                Name = "open",
                TileType = openTileType,
                Settings = openTileType.Settings
            };

            // Register tile and terrain types
            world.AddTileTypes(new List<TileType> { openTileType });
            world.AddTerrainTypes(new List<TerrainType> { openTerrainType });

            return world;
        }

        /// <summary>
        /// Fills terrain in the specified bounds area for FOV calculations.
        /// </summary>
        private void FillTerrain(World world, Rectangle bounds)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    world.SetTerrain("open", new WorldLocation(x, y, 0));
                }
            }
        }

        [Fact]
        public void DirectionalFov_OriginIsAlwaysVisible()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(5, 5, 11, 11); // 11x11 area centered on origin
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - facing North (0°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 0, fovDegrees: 90);

            // Assert - origin cell should always be visible
            int originOffsetY = origin.Y - bounds.Y;
            int originOffsetX = origin.X - bounds.X;
            Assert.True(visible[originOffsetY, originOffsetX], "Origin should always be visible");
        }

        [Fact]
        public void DirectionalFov_360Degrees_IsOmnidirectional()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(8, 8, 5, 5); // Small 5x5 area
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - 360° FOV should see everything
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 0, fovDegrees: 360);

            // Assert - all cells in range should be visible
            for (int y = 0; y < bounds.Height; y++)
            {
                for (int x = 0; x < bounds.Width; x++)
                {
                    Assert.True(visible[y, x], $"Cell ({x}, {y}) should be visible with 360° FOV");
                }
            }
        }

        [Fact]
        public void DirectionalFov_FacingNorth_SeesNorth()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(8, 6, 5, 9); // Area covering cells north and south
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing North (0°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 0, fovDegrees: 90);

            // Assert
            // Cell directly north of origin (10, 9) should be visible
            int northCellY = 9 - bounds.Y; // offset in visible array
            int northCellX = 10 - bounds.X;
            Assert.True(visible[northCellY, northCellX], "Cell directly north should be visible when facing north");

            // Cell directly south of origin (10, 11) should NOT be visible
            int southCellY = 11 - bounds.Y;
            int southCellX = 10 - bounds.X;
            Assert.False(visible[southCellY, southCellX], "Cell directly south should NOT be visible when facing north with 90° FOV");
        }

        [Fact]
        public void DirectionalFov_FacingEast_SeesEast()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(6, 8, 9, 5); // Area covering cells east and west
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing East (90°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 90, fovDegrees: 90);

            // Assert
            // Cell directly east (11, 10) should be visible
            int eastCellY = 10 - bounds.Y;
            int eastCellX = 11 - bounds.X;
            Assert.True(visible[eastCellY, eastCellX], "Cell directly east should be visible when facing east");

            // Cell directly west (9, 10) should NOT be visible
            int westCellY = 10 - bounds.Y;
            int westCellX = 9 - bounds.X;
            Assert.False(visible[westCellY, westCellX], "Cell directly west should NOT be visible when facing east with 90° FOV");
        }

        [Fact]
        public void DirectionalFov_FacingSouth_SeesSouth()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(8, 6, 5, 9);
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing South (180°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 180, fovDegrees: 90);

            // Assert
            // Cell directly south (10, 11) should be visible
            int southCellY = 11 - bounds.Y;
            int southCellX = 10 - bounds.X;
            Assert.True(visible[southCellY, southCellX], "Cell directly south should be visible when facing south");

            // Cell directly north (10, 9) should NOT be visible
            int northCellY = 9 - bounds.Y;
            int northCellX = 10 - bounds.X;
            Assert.False(visible[northCellY, northCellX], "Cell directly north should NOT be visible when facing south with 90° FOV");
        }

        [Fact]
        public void DirectionalFov_FacingWest_SeesWest()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(6, 8, 9, 5);
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing West (270°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 270, fovDegrees: 90);

            // Assert
            // Cell directly west (9, 10) should be visible
            int westCellY = 10 - bounds.Y;
            int westCellX = 9 - bounds.X;
            Assert.True(visible[westCellY, westCellX], "Cell directly west should be visible when facing west");

            // Cell directly east (11, 10) should NOT be visible
            int eastCellY = 10 - bounds.Y;
            int eastCellX = 11 - bounds.X;
            Assert.False(visible[eastCellY, eastCellX], "Cell directly east should NOT be visible when facing west with 90° FOV");
        }

        [Fact]
        public void DirectionalFov_120DegreeFov_WiderCone()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(7, 7, 7, 7); // 7x7 grid
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing North (0°) with 120° FOV (human-like)
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 0, fovDegrees: 120);

            // Assert - 120° FOV should see more to the sides than 90°
            // Cells at 45° angle should be visible
            int northEastY = 9 - bounds.Y; // (11, 9) is northeast
            int northEastX = 11 - bounds.X;
            Assert.True(visible[northEastY, northEastX], "Northeast cell should be visible with 120° FOV facing north");

            // But directly behind should still be invisible
            int southY = 11 - bounds.Y;
            int southX = 10 - bounds.X;
            Assert.False(visible[southY, southX], "South cell should NOT be visible with 120° FOV facing north");
        }

        [Fact]
        public void DirectionalFov_270DegreeFov_VeryWideCone()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(7, 7, 7, 7);
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing North (0°) with 270° FOV (very wide, like prey animal)
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 0, fovDegrees: 270);

            // Assert - Should see most directions except directly behind
            // Northeast, East, West should all be visible
            int eastY = 10 - bounds.Y;
            int eastX = 11 - bounds.X;
            Assert.True(visible[eastY, eastX], "East should be visible with 270° FOV");

            int westY = 10 - bounds.Y;
            int westX = 9 - bounds.X;
            Assert.True(visible[westY, westX], "West should be visible with 270° FOV");

            // But directly south should be barely outside the cone
            int southY = 11 - bounds.Y;
            int southX = 10 - bounds.X;
            Assert.False(visible[southY, southX], "Directly south should NOT be visible with 270° FOV facing north");
        }

        [Fact]
        public void DirectionalFov_45DegreeHeading_DiagonalView()
        {
            // Arrange
            var world = CreateTestWorld();
            var origin = new WorldLocation(10, 10, 0);
            var bounds = new Rectangle(7, 7, 7, 7);
            FillTerrain(world, bounds);
            var calculator = new DirectionalFovCalculator();

            // Act - Facing Northeast (45°) with 90° FOV
            var visible = calculator.ComputeVisible(world, origin, bounds, 10, headingDegrees: 45, fovDegrees: 90);

            // Assert
            // Cell to the northeast should be visible
            int neY = 9 - bounds.Y;
            int neX = 11 - bounds.X;
            Assert.True(visible[neY, neX], "Northeast cell should be visible when facing northeast");

            // Cell to the southwest should NOT be visible
            int swY = 11 - bounds.Y;
            int swX = 9 - bounds.X;
            Assert.False(visible[swY, swX], "Southwest cell should NOT be visible when facing northeast");
        }
    }
}


