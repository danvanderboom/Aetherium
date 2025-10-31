using System;
using System.Linq;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.WorldBuilders
{
    public class TestMazeWorldBuilder : WorldBuilder
    {
        public TestMazeWorldBuilder() : base() { }

        public override World Build()
        {
            var world = new World();

            world.AddTileTypes(TileTypes);
            world.AddTerrainTypes(CreateTerrainTypes(TileTypes));

            // Build a 30x30 test maze at z=0
            BuildMaze(world);

            return world;
        }

        public void BuildMaze(World world)
        {
            const int size = 30;
            const int z = 0;

            // Fill with walls first
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, z));
                }
            }

            // Central open room (10x10, centered)
            for (int y = 10; y < 20; y++)
            {
                for (int x = 10; x < 20; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, z));
                }
            }

            // Horizontal corridor from west (0,15) to central room
            for (int x = 0; x < 10; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 15, z));
            }

            // Horizontal corridor from east (20,15) to edge
            for (int x = 20; x < size; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 15, z));
            }

            // Vertical corridor from north (15,0) to central room
            for (int y = 0; y < 10; y++)
            {
                world.SetTerrain("Indoors", new WorldLocation(15, y, z));
            }

            // Vertical corridor from south (15,20) to edge
            for (int y = 20; y < size; y++)
            {
                world.SetTerrain("Indoors", new WorldLocation(15, y, z));
            }

            // L-shaped corridor in top-left corner for corner occlusion testing
            // Horizontal part: (5,5) to (10,5)
            for (int x = 5; x <= 10; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 5, z));
            }
            // Vertical part: (5,5) to (5,10)
            for (int y = 5; y <= 10; y++)
            {
                world.SetTerrain("Indoors", new WorldLocation(5, y, z));
            }

            // Forest patch in corridor (x=7-9, y=15) for opacity testing
            // Override the Indoors terrain we just set
            world.SetTerrain("Forest", new WorldLocation(7, 15, z));
            world.SetTerrain("Forest", new WorldLocation(8, 15, z));
            world.SetTerrain("Forest", new WorldLocation(9, 15, z));

            // Water patch (x=21-23, y=15) for vision-through-movement-block
            // Override the Indoors terrain we just set
            world.SetTerrain("Water", new WorldLocation(21, 15, z));
            world.SetTerrain("Water", new WorldLocation(22, 15, z));
            world.SetTerrain("Water", new WorldLocation(23, 15, z));

            // Door in east corridor (closed by default)
            var door1 = new Door();
            door1.Set(new WorldLocation(25, 15, z));
            world.AddEntity(door1);

            // Another door in south corridor (also closed)
            var door2 = new Door();
            door2.Set(new WorldLocation(15, 25, z));
            world.AddEntity(door2);

            // Small room in top-right corner connected by corridor
            for (int y = 5; y < 10; y++)
            {
                for (int x = 25; x < 30; x++)
                {
                    world.SetTerrain("Indoors", new WorldLocation(x, y, z));
                }
            }
            // Corridor from central room to top-right room
            for (int x = 20; x < 25; x++)
            {
                world.SetTerrain("Indoors", new WorldLocation(x, 7, z));
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
                    { "MapCharacter", "|" },
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
    }
}


