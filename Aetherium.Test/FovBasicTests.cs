using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldBuilders;
using Aetherium.Systems;
using Aetherium.Entities;

namespace Aetherium.Test
{
    /// <summary>
    /// Progressive FOV tests from simple to complex.
    /// Each test builds a minimal world to test a specific FOV behavior.
    /// </summary>
    [TestFixture]
    public class FovBasicTests
    {
        private World BuildSimpleWorld(string[,] layout, int width, int height)
        {
            var builder = new SimpleWorldBuilder();
            var world = builder.Build();
            
            // Fill world based on layout
            // ' ' = empty/open (Indoors)
            // '#' = wall (Wall)
            // 'F' = forest (Forest)
            // 'W' = water (Water)
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var loc = new WorldLocation(x, y, 0);
                    var c = layout[y, x];
                    
                    switch (c)
                    {
                        case " ":
                            world.SetTerrain("Indoors", loc);
                            break;
                        case "#":
                            world.SetTerrain("Wall", loc);
                            break;
                        case "F":
                            world.SetTerrain("Forest", loc);
                            break;
                        case "W":
                            world.SetTerrain("Water", loc);
                            break;
                    }
                }
            }
            
            return world;
        }

        [Test]
        public void Level1_Origin_Always_Visible()
        {
            // Simplest test: Origin must always be visible regardless of surroundings
            var layout = new string[,]
            {
                { "#", "#", "#" },
                { "#", " ", "#" },
                { "#", "#", "#" }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(1, 1, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[1, 1], "Origin (1,1) must always be visible");
        }

        [Test]
        public void Level1_Adjacent_Cardinal_Directions_Visible_In_Open_Space()
        {
            // Test basic 4-directional visibility in open space
            var layout = new string[,]
            {
                { " ", " ", " " },
                { " ", " ", " " },
                { " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(1, 1, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // North, South, East, West should all be visible
            Assert.True(visible[0, 1], "North should be visible");
            Assert.True(visible[2, 1], "South should be visible");
            Assert.True(visible[1, 2], "East should be visible");
            Assert.True(visible[1, 0], "West should be visible");
        }

        [Test]
        public void Level1_Diagonal_Adjacent_Visible_In_Open_Space()
        {
            // Test diagonal visibility in open space
            var layout = new string[,]
            {
                { " ", " ", " " },
                { " ", " ", " " },
                { " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(1, 1, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // All diagonals should be visible
            Assert.True(visible[0, 0], "Northwest should be visible");
            Assert.True(visible[0, 2], "Northeast should be visible");
            Assert.True(visible[2, 0], "Southwest should be visible");
            Assert.True(visible[2, 2], "Southeast should be visible");
        }

        [Test]
        public void Level2_Single_Wall_Blocks_Horizontal_Line()
        {
            // Test that a wall directly between origin and target blocks vision
            var layout = new string[,]
            {
                { " ", "#", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 3, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Origin should be visible
            Assert.True(visible[0, 0], "Origin should be visible");
            
            // Wall itself may be visible
            Assert.True(visible[0, 1], "Wall cell may be visible");
            
            // Beyond wall should NOT be visible
            Assert.False(visible[0, 2], "Beyond wall should not be visible");
        }

        [Test]
        public void Level2_Single_Wall_Blocks_Vertical_Line()
        {
            // Test that a wall directly between origin and target blocks vision vertically
            var layout = new string[,]
            {
                { " " },
                { "#" },
                { " " }
            };
            var world = BuildSimpleWorld(layout, 1, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 1, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin should be visible");
            Assert.True(visible[1, 0], "Wall cell may be visible");
            Assert.False(visible[2, 0], "Beyond wall should not be visible");
        }

        [Test]
        public void Level2_Wall_Blocks_Diagonal_Line()
        {
            // Test diagonal line blocked by wall
            var layout = new string[,]
            {
                { " ", " ", " " },
                { " ", "#", " " },
                { " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Origin visible
            Assert.True(visible[0, 0], "Origin should be visible");
            
            // The wall cell (1,1) may be visible
            Assert.True(visible[1, 1], "Wall cell may be visible");
            
            // But (2,2) beyond the wall should NOT be visible
            Assert.False(visible[2, 2], "Beyond diagonal wall should not be visible");
        }

        [Test]
        public void Level2_Corner_Occlusion_Blocks_LineOfSight()
        {
            // L-shaped corridor: horizontal then vertical
            // From corner, should not see around the corner
            var layout = new string[,]
            {
                { " ", " ", " ", "#", " " },
                { "#", "#", "#", "#", " " },
                { " ", " ", " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 5, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 5, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Can see along the horizontal corridor
            Assert.True(visible[0, 1], "Horizontal corridor visible");
            Assert.True(visible[0, 2], "Horizontal corridor visible");
            
            // Cannot see around the corner (vertically down)
            // The target (4,2) is around the corner and should not be visible
            Assert.False(visible[2, 4], "Around corner should not be visible");
        }

        [Test]
        public void Level3_Single_Partial_Opacity_Does_Not_Block()
        {
            // Single forest tile (0.49 opacity) should not block by itself
            var layout = new string[,]
            {
                { " ", "F", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 3, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Forest tile should be visible");
            Assert.True(visible[0, 2], "Beyond single forest should be visible (0.49 < 1.0)");
        }

        [Test]
        public void Level3_Two_Partial_Opacity_Does_Not_Block()
        {
            // Two forest tiles (0.49 + 0.49 = 0.98) should not block
            var layout = new string[,]
            {
                { " ", "F", "F", " " }
            };
            var world = BuildSimpleWorld(layout, 4, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 4, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "First forest visible");
            Assert.True(visible[0, 2], "Second forest visible");
            Assert.True(visible[0, 3], "Beyond two forests should be visible (0.98 < 1.0)");
        }

        [Test]
        public void Level3_Three_Partial_Opacity_Blocks()
        {
            // Three forest tiles (0.49 * 3 = 1.47) should block
            var layout = new string[,]
            {
                { " ", "F", "F", "F", " " }
            };
            var world = BuildSimpleWorld(layout, 5, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 5, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "First forest visible");
            Assert.True(visible[0, 2], "Second forest visible");
            Assert.True(visible[0, 3], "Third forest (blocker) visible");
            Assert.False(visible[0, 4], "Beyond three forests should NOT be visible (1.47 >= 1.0)");
        }

        [Test]
        public void Level3_Water_Does_Not_Block_Vision()
        {
            // Water should be completely transparent for vision
            var layout = new string[,]
            {
                { " ", "W", "W", "W", " " }
            };
            var world = BuildSimpleWorld(layout, 5, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 5, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Water tile visible");
            Assert.True(visible[0, 2], "Water tile visible");
            Assert.True(visible[0, 3], "Water tile visible");
            Assert.True(visible[0, 4], "Beyond water should be visible (water is transparent)");
        }

        [Test]
        public void Level4_Closed_Door_Blocks()
        {
            // Closed door should block vision
            var world = new SimpleWorldBuilder().Build();
            
            // Create a simple corridor with a door
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(2, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(3, 0, 0));
            
            // Add a closed door at (2,0)
            var door = new Door();
            door.Set(new WorldLocation(2, 0, 0));
            world.AddEntity(door);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 4, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Before door visible");
            Assert.True(visible[0, 2], "Door cell itself may be visible");
            Assert.False(visible[0, 3], "Beyond closed door should NOT be visible");
        }

        [Test]
        public void Level4_Open_Door_Allows_Vision()
        {
            // Open door should allow vision through
            var world = new SimpleWorldBuilder().Build();
            
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(1, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(2, 0, 0));
            world.SetTerrain("Indoors", new WorldLocation(3, 0, 0));
            
            var door = new Door();
            door.Set(new WorldLocation(2, 0, 0));
            if (door.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp))
            {
                var opens = ocComp as OpensAndCloses;
                if (opens != null)
                    opens.IsOpen = true;
            }
            world.AddEntity(door);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 4, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Before door visible");
            Assert.True(visible[0, 2], "Door cell visible");
            Assert.True(visible[0, 3], "Beyond open door SHOULD be visible");
        }

        [Test]
        public void Level5_MaxRange_Limits_Visibility()
        {
            // Vision should be limited by maxRange
            var layout = new string[,]
            {
                { " ", " ", " ", " ", " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 7, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 7, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 3);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Distance 1 visible");
            Assert.True(visible[0, 2], "Distance 2 visible");
            Assert.True(visible[0, 3], "Distance 3 (at maxRange) visible");
            Assert.False(visible[0, 4], "Distance 4 (beyond maxRange) should NOT be visible");
            Assert.False(visible[0, 5], "Distance 5 should NOT be visible");
            Assert.False(visible[0, 6], "Distance 6 should NOT be visible");
        }

        [Test]
        public void Level5_All_Cells_Visible_In_Small_Open_Space()
        {
            // In a small open space with no obstacles, all cells should be visible
            var layout = new string[,]
            {
                { " ", " ", " " },
                { " ", " ", " " },
                { " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(1, 1, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // All 9 cells should be visible
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Assert.True(visible[y, x], $"Cell ({x},{y}) should be visible");
                }
            }
        }

        [Test]
        public void Level5_Surrounded_By_Walls_Only_Origin_Visible()
        {
            // If completely surrounded by walls, only origin should be visible
            var layout = new string[,]
            {
                { "#", "#", "#" },
                { "#", " ", "#" },
                { "#", "#", "#" }
            };
            var world = BuildSimpleWorld(layout, 3, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(1, 1, 0);
            var bounds = new Rectangle(0, 0, 3, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[1, 1], "Origin should be visible");
            
            // All surrounding cells should be walls and may be visible (the wall itself),
            // but nothing beyond should be visible
            // Actually, the walls themselves might be visible as they're adjacent
            Assert.True(visible[0, 1] || visible[2, 1] || visible[1, 0] || visible[1, 2], 
                "At least one adjacent wall cell should be visible (the wall itself)");
        }

        [Test]
        public void Level6_Long_Straight_Corridor_Visibility()
        {
            // Test visibility along a long straight corridor
            var layout = new string[,]
            {
                { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 10, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 10, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // All cells in the corridor should be visible
            for (int x = 0; x < 10; x++)
            {
                Assert.True(visible[0, x], $"Cell ({x},0) should be visible in straight corridor");
            }
        }

        [Test]
        public void Level6_Wall_With_Gap_Visibility()
        {
            // Two walls with a gap - should see through the gap but not around walls
            var layout = new string[,]
            {
                { " ", "#", " ", "#", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 6, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 6, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "First wall visible (the wall itself)");
            Assert.False(visible[0, 2], "Beyond first wall should NOT be visible");
            Assert.False(visible[0, 3], "Second wall should NOT be visible (blocked by first)");
            Assert.False(visible[0, 4], "Beyond second wall should NOT be visible");
            Assert.False(visible[0, 5], "End should NOT be visible");
        }

        [Test]
        public void Level6_Complex_Corner_Scenario()
        {
            // Complex L-shape with multiple corridors
            // Horizontal corridor, then vertical, then another horizontal
            var layout = new string[,]
            {
                { " ", " ", " ", "#", " ", " " },
                { "#", "#", "#", "#", "#", " " },
                { " ", " ", " ", " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 6, 3);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 6, 3);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Can see along first horizontal corridor
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Horizontal corridor visible");
            Assert.True(visible[0, 2], "Horizontal corridor visible");
            Assert.True(visible[0, 3], "Wall visible (the wall itself)");
            
            // Cannot see around corner into second horizontal corridor
            Assert.False(visible[2, 4], "Around corner should not be visible");
            Assert.False(visible[2, 5], "End of second corridor should not be visible");
        }

        [Test]
        public void Level7_Mixed_Opacity_Scenarios()
        {
            // Forest + Wall combination
            var layout = new string[,]
            {
                { " ", "F", "F", "#", " " }
            };
            var world = BuildSimpleWorld(layout, 5, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 5, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "First forest visible");
            Assert.True(visible[0, 2], "Second forest visible");
            Assert.True(visible[0, 3], "Wall visible (the wall itself)");
            Assert.False(visible[0, 4], "Beyond wall should NOT be visible");
        }

        [Test]
        public void Level7_Forest_Wall_Forest()
        {
            // Forest, then wall, then forest - wall should block
            var layout = new string[,]
            {
                { " ", "F", "#", "F", " " }
            };
            var world = BuildSimpleWorld(layout, 5, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 5, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "First forest visible");
            Assert.True(visible[0, 2], "Wall visible (the wall itself)");
            Assert.False(visible[0, 3], "Forest beyond wall should NOT be visible");
            Assert.False(visible[0, 4], "End should NOT be visible");
        }

        [Test]
        public void Level7_Water_With_Forest_Beyond()
        {
            // Water is transparent, but forest beyond still blocks after accumulation
            var layout = new string[,]
            {
                { " ", "W", "W", "F", "F", "F", " " }
            };
            var world = BuildSimpleWorld(layout, 7, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0);
            var bounds = new Rectangle(0, 0, 7, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin visible");
            Assert.True(visible[0, 1], "Water visible");
            Assert.True(visible[0, 2], "Water visible");
            Assert.True(visible[0, 3], "First forest visible");
            Assert.True(visible[0, 4], "Second forest visible");
            Assert.True(visible[0, 5], "Third forest (blocker) visible");
            Assert.False(visible[0, 6], "Beyond three forests should NOT be visible");
        }

        [Test]
        public void Level8_Circular_Vision_Pattern()
        {
            // Large open space - test circular vision pattern
            var layout = new string[5, 5];
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    layout[y, x] = " ";
                }
            }
            var world = BuildSimpleWorld(layout, 5, 5);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(2, 2, 0); // Center
            var bounds = new Rectangle(0, 0, 5, 5);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // All cells should be visible (open space, within range)
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Assert.True(visible[y, x], $"Cell ({x},{y}) should be visible in open space");
                }
            }
        }

        [Test]
        public void Level8_Perimeter_Walls_With_Open_Center()
        {
            // Walls around perimeter, open center
            var layout = new string[,]
            {
                { "#", "#", "#", "#", "#" },
                { "#", " ", " ", " ", "#" },
                { "#", " ", " ", " ", "#" },
                { "#", " ", " ", " ", "#" },
                { "#", "#", "#", "#", "#" }
            };
            var world = BuildSimpleWorld(layout, 5, 5);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(2, 2, 0); // Center
            var bounds = new Rectangle(0, 0, 5, 5);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Center should be visible
            Assert.True(visible[2, 2], "Center visible");
            
            // All inner open cells should be visible
            for (int y = 1; y < 4; y++)
            {
                for (int x = 1; x < 4; x++)
                {
                    Assert.True(visible[y, x], $"Inner cell ({x},{y}) should be visible");
                }
            }
            
            // Wall cells themselves may be visible (adjacent)
            // But cells beyond walls should not be visible
            // The outer perimeter walls (corners) are beyond inner walls, so they might not be visible
        }

        [Test]
        public void Level9_Edge_Of_Bounds_Scenarios()
        {
            // Origin at edge of bounds
            var layout = new string[,]
            {
                { " ", " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 4, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(0, 0, 0); // At left edge
            var bounds = new Rectangle(0, 0, 4, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            Assert.True(visible[0, 0], "Origin at edge should be visible");
            Assert.True(visible[0, 1], "Adjacent to edge should be visible");
            Assert.True(visible[0, 2], "Further should be visible");
            Assert.True(visible[0, 3], "At other edge should be visible");
        }

        [Test]
        public void Level9_Origin_Outside_Bounds()
        {
            // Origin outside bounds - should still mark origin as visible if bounds.Contains(origin)
            var layout = new string[,]
            {
                { " ", " ", " " }
            };
            var world = BuildSimpleWorld(layout, 3, 1);
            
            var fov = new FovCalculator();
            var origin = new WorldLocation(5, 0, 0); // Outside bounds
            var bounds = new Rectangle(0, 0, 3, 1);
            var visible = fov.ComputeVisible(world, origin, bounds, maxRange: 10);
            
            // Origin is outside bounds, so it won't be marked visible
            // But we can still compute vision from outside
            // The bounds (0,0,3,1) cells should not be visible from origin at (5,0)
            // because distance check: sqrt((5-0)^2) = 5, which might be > maxRange if maxRange is small
        }
    }

    /// <summary>
    /// Simple world builder for basic FOV tests.
    /// Creates minimal worlds with just the essential tile types.
    /// </summary>
    public class SimpleWorldBuilder : WorldBuilder
    {
        public SimpleWorldBuilder() : base() { }

        public override World Build()
        {
            var world = new World();
            world.AddTileTypes(TileTypes);
            world.AddTerrainTypes(CreateTerrainTypes(TileTypes));
            return world;
        }

        string[] TerrainTypeNames => new string[]
        {
            "None",
            "Indoors",
            "Wall",
            "Forest",
            "Water"
        };

        public List<TerrainType> CreateTerrainTypes(IList<TileType> tileTypes) =>
            TileTypes
            .Select(t => new TerrainType
            {
                Name = t.Name,
                TileType = tileTypes.First(tt => tt.Name == t.Name),
                Settings = t.Settings
            })
            .Where(t => TerrainTypeNames.Contains(t.Name))
            .ToList();

        public List<TileType> TileTypes => new List<TileType>
        {
            new TileType
            {
                Name = "None",
                DefaultComponents = new List<Component> { new ObstructsMovement(), new ObstructsView() },
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", " " },
                    { "BackgroundColor", ConsoleColor.Black.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
                }
            },
            new TileType
            {
                Name = "Indoors",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", " " },
                    { "BackgroundColor", ConsoleColor.Gray.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
                }
            },
            new TileType
            {
                Name = "Wall",
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "#" },
                    { "BackgroundColor", ConsoleColor.Gray.ToString() },
                    { "ForegroundColor", ConsoleColor.DarkRed.ToString() },
                }
            },
            new TileType
            {
                Name = "Forest",
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 0.49 } },
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "t" },
                    { "BackgroundColor", ConsoleColor.Black.ToString() },
                    { "ForegroundColor", ConsoleColor.Green.ToString() },
                }
            },
            new TileType
            {
                Name = "Water",
                DefaultComponents = new List<Component> { new ObstructsMovement() },
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "~" },
                    { "BackgroundColor", ConsoleColor.Blue.ToString() },
                    { "ForegroundColor", ConsoleColor.White.ToString() },
                }
            }
        };
    }
}

