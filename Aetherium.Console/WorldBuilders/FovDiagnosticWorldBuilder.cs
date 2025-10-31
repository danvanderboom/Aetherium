using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Lighting;

namespace Aetherium.WorldBuilders
{
    /// <summary>
    /// Creates diagnostic test maps for FOV visualization and testing.
    /// Each map tests specific FOV scenarios.
    /// </summary>
    public class FovDiagnosticWorldBuilder : WorldBuilder
    {
        private readonly string _testMapName;

        public FovDiagnosticWorldBuilder(string testMapName)
        {
            _testMapName = testMapName;
        }

        public override World Build()
        {
            var world = new World();
            world.AddTileTypes(TileTypes);
            world.AddTerrainTypes(CreateTerrainTypes(TileTypes));

            switch (_testMapName.ToLowerInvariant())
            {
                case "open_space":
                    BuildOpenSpaceTest(world);
                    AddLightSourceAtCenter(world, 15, 15, 0); // Add light at player start position
                    break;
                case "simple_wall":
                    BuildSimpleWallTest(world);
                    break;
                case "corner_occlusion":
                    BuildCornerOcclusionTest(world);
                    break;
                case "partial_opacity":
                    BuildPartialOpacityTest(world);
                    break;
                case "door_test":
                    BuildDoorTest(world);
                    break;
                case "multiwall":
                    BuildMultiWallTest(world);
                    break;
                case "diagonal_wall":
                    BuildDiagonalWallTest(world);
                    break;
                case "cross_hair":
                    BuildCrossHairTest(world);
                    break;
                case "chamber":
                    BuildChamberTest(world);
                    break;
                default:
                    BuildOpenSpaceTest(world);
                    break;
            }

            return world;
        }

        // Test 0: Open space - simple open area with good visibility from center
        private void BuildOpenSpaceTest(World world)
        {
            // Create a 30x30 open area with a few features for visual interest
            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 30; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }
            
            // Add a few walls for visual interest (but not blocking the center)
            // A small room in the corner
            for (int y = 5; y < 12; y++)
            {
                for (int x = 5; x < 12; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }
            // Walls around the small room
            for (int y = 4; y < 13; y++)
            {
                world.SetTerrain("Wall", new WorldLocation(4, y, 0));
                world.SetTerrain("Wall", new WorldLocation(12, y, 0));
            }
            for (int x = 5; x < 12; x++)
            {
                world.SetTerrain("Wall", new WorldLocation(x, 4, 0));
                world.SetTerrain("Wall", new WorldLocation(x, 12, 0));
            }
            
            // Another feature - some forest tiles in a corner
            for (int y = 20; y < 25; y++)
            {
                for (int x = 20; x < 25; x++)
                {
                    world.SetTerrain("Forest", new WorldLocation(x, y, 0));
                }
            }
        }

        // Test 1: Simple wall blocking - player at (5,5), wall at (10,5), corridor extends to (15,5)
        private void BuildSimpleWallTest(World world)
        {
            // Horizontal corridor with a wall in the middle
            for (int x = 0; x < 20; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, 0));
            }
            world.SetTerrain("Wall", new WorldLocation(10, 5, 0));
        }

        // Test 2: L-shaped corridor - corner should block vision around it
        private void BuildCornerOcclusionTest(World world)
        {
            // Horizontal corridor (0,5) to (10,5)
            for (int x = 0; x <= 10; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, 0));
            }
            // Vertical corridor (10,5) to (10,10)
            for (int y = 5; y <= 10; y++)
            {
                world.SetTerrain("Indoors", new WorldLocation(10, y, 0));
            }
            // Wall forming the corner
            world.SetTerrain("Wall", new WorldLocation(10, 5, 0));
        }

        // Test 3: Forest opacity accumulation - player at (5,15), forests at (8-10,15)
        private void BuildPartialOpacityTest(World world)
        {
            for (int x = 0; x < 20; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 15, 0));
            }
            world.SetTerrain("Forest", new WorldLocation(8, 15, 0));
            world.SetTerrain("Forest", new WorldLocation(9, 15, 0));
            world.SetTerrain("Forest", new WorldLocation(10, 15, 0));
            world.SetTerrain("Forest", new WorldLocation(11, 15, 0));
        }

        // Test 4: Door opening/closing - corridor with door at (10,5)
        private void BuildDoorTest(World world)
        {
            for (int x = 0; x < 20; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, 0));
            }
            var door = new Door();
            door.Set(new WorldLocation(10, 5, 0));
            world.AddEntity(door);
        }

        // Test 5: Multiple walls in sequence
        private void BuildMultiWallTest(World world)
        {
            for (int x = 0; x < 20; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 10, 0));
            }
            world.SetTerrain("Wall", new WorldLocation(5, 10, 0));
            world.SetTerrain("Wall", new WorldLocation(10, 10, 0));
            world.SetTerrain("Wall", new WorldLocation(15, 10, 0));
        }

        // Test 6: Diagonal wall blocking diagonal line of sight
        private void BuildDiagonalWallTest(World world)
        {
            // Open space with a diagonal wall
            for (int y = 0; y < 15; y++)
            {
                for (int x = 0; x < 15; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }
            // Diagonal wall
            world.SetTerrain("Wall", new WorldLocation(5, 5, 0));
            world.SetTerrain("Wall", new WorldLocation(6, 6, 0));
            world.SetTerrain("Wall", new WorldLocation(7, 7, 0));
        }

        // Test 7: Cross-hair pattern - player at center, walls at cardinal directions
        private void BuildCrossHairTest(World world)
        {
            // Open space
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }
            // Walls blocking cardinal directions from center
            world.SetTerrain("Wall", new WorldLocation(10, 5, 0));  // North
            world.SetTerrain("Wall", new WorldLocation(10, 15, 0)); // South
            world.SetTerrain("Wall", new WorldLocation(5, 10, 0));  // West
            world.SetTerrain("Wall", new WorldLocation(15, 10, 0)); // East
        }

        // Test 8: Room with one exit - player inside, corridor outside
        private void BuildChamberTest(World world)
        {
            // Room (5-14, 5-14)
            for (int y = 5; y < 15; y++)
            {
                for (int x = 5; x < 15; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));
                }
            }
            // Walls around room
            for (int y = 4; y < 16; y++)
            {
                world.SetTerrain("Wall", new WorldLocation(4, y, 0));
                world.SetTerrain("Wall", new WorldLocation(15, y, 0));
            }
            for (int x = 5; x < 15; x++)
            {
                world.SetTerrain("Wall", new WorldLocation(x, 4, 0));
                world.SetTerrain("Wall", new WorldLocation(x, 15, 0));
            }
            // Exit corridor to the east
            for (int x = 15; x < 25; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 10, 0));
            }
        }

        string[] TerrainTypeNames => new string[]
        {
            "None",
            "Indoors",
            "Wall",
            "Mountain",
            "Road",
            "Plains",
            "Forest",
            "Water",
            "Cave",
            "Upstairs",
            "Downstairs"
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
                Name = "Mountain",
                DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "^" },
                    { "BackgroundColor", ConsoleColor.DarkGray.ToString() },
                    { "ForegroundColor", ConsoleColor.White.ToString() },
                }
            },
            new TileType
            {
                Name = "Road",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "=" },
                    { "BackgroundColor", ConsoleColor.Black.ToString() },
                    { "ForegroundColor", ConsoleColor.White.ToString() },
                }
            },
            new TileType
            {
                Name = "Plains",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "." },
                    { "BackgroundColor", ConsoleColor.DarkYellow.ToString() },
                    { "ForegroundColor", ConsoleColor.Yellow.ToString() },
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
            },
            new TileType
            {
                Name = "Cave",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "t" },
                    { "BackgroundColor", ConsoleColor.Black.ToString() },
                    { "ForegroundColor", ConsoleColor.DarkGray.ToString() },
                }
            },
            new TileType
            {
                Name = "Upstairs",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "+" },
                    { "BackgroundColor", ConsoleColor.Gray.ToString() },
                    { "ForegroundColor", ConsoleColor.Yellow.ToString() },
                }
            },
            new TileType
            {
                Name = "Downstairs",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "-" },
                    { "BackgroundColor", ConsoleColor.Gray.ToString() },
                    { "ForegroundColor", ConsoleColor.Yellow.ToString() },
                }
            },
            new TileType
            {
                Name = "Player",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "*" },
                    { "BackgroundColor", ConsoleColor.White.ToString() },
                    { "ForegroundColor", ConsoleColor.Blue.ToString() },
                }
            }
        };

        /// <summary>
        /// Adds a light source at the specified location for visibility testing.
        /// </summary>
        private void AddLightSourceAtCenter(World world, int x, int y, int z)
        {
            var lightEntity = new LightEntity();
            lightEntity.Set(new LightSource(1.0, 50)); // Full intensity, long range
            lightEntity.Set(new WorldLocation(x, y, z));
            world.AddEntity(lightEntity);
        }
    }
}


