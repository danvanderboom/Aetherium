using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame.Views;

namespace ConsoleGame.Core
{
    public class ConsoleDungeonGame
    {
        public World World { get; protected set; }

        public Character player { get; protected set; }

        List<ConsoleView> Views;

        ConsoleMapView mapView;

        ConsoleColor oldBackgroundColor;
        ConsoleColor oldForegroundColor;

        Random rand = new Random();

        public ConsoleDungeonGame() => Initialize();

        public void Initialize()
        {
            CreateWorld();

            Views = new List<ConsoleView>();

            mapView = new ConsoleMapView
            {
                ScreenPosition = new Point(3, 2),
                Size = new Size(41, 21),
                BackgroundColor = ConsoleColor.Black,
                HasFrame = true,
                FrameBackgroundColor = ConsoleColor.DarkGray,
                FrameForegroundColor = ConsoleColor.Black,
                TileTypes = CreateTileTypes(),
                World = this.World
            };

            Views.Add(mapView);

            var locations = World.EntitiesByLocation.Keys.ToList();
            if (locations.Any())
            {
                var randomLocation = locations[rand.Next(0, locations.Count)];
                if (randomLocation != null)
                    mapView.WorldLocation = randomLocation;
            }
        }

        private void CreateWorld()
        {
            World = new World();
            World.AddTileTypes(CreateTileTypes());
            World.AddTerrainTypes(CreateTerrainTypes());

            var builder = new DungeonCrawlerWorldBuilder(World);
            builder.Build();
        }

        public void Run()
        {
            Console.CursorVisible = false;
            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            Clear(ConsoleColor.Black);

            DisplayViews();

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, -1, 0);
                        break;
                    case ConsoleKey.DownArrow:
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, +1, 0);
                        break;
                    case ConsoleKey.LeftArrow:
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(-1, 0, 0);
                        break;
                    case ConsoleKey.RightArrow:
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(+1, 0, 0);
                        break;
                    case ConsoleKey.C:
                        Clear(mapView.ContentScreenPosition, mapView.ContentSize, ConsoleColor.DarkYellow);
                        continue;
                }

                DisplayViewContents();
            }

            Console.CursorVisible = false;
            Console.BackgroundColor = oldBackgroundColor;
            Console.ForegroundColor = oldForegroundColor;
        }

        public void DisplayViews()
        {
            foreach (var view in Views)
            {
                view.DrawFrame();
                view.DrawContents();
            }
        }

        public void DisplayViewContents()
        {
            foreach (var view in Views)
                view.DrawContents();
        }

        void Clear(Point location, Size size, ConsoleColor backgroundColor)
        {
            Console.BackgroundColor = backgroundColor;

            for (int y = location.Y; y < location.Y + size.Height; y++)
            {
                Console.SetCursorPosition(location.X, y);
                Console.Write(new string(' ', size.Width));
            }
        }

        void Clear(ConsoleColor? backgroundColor = null)
        {
            if (backgroundColor.HasValue)
            {
                oldBackgroundColor = backgroundColor.Value;
                Console.BackgroundColor = backgroundColor.Value;
            }

            Console.Clear();
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

        public List<TerrainType> CreateTerrainTypes() =>
            CreateTileTypes()
            .Select(t => new TerrainType 
            { 
                Name = t.Name, 
                TileType = World.TileTypes[t.Name], 
                Settings = t.Settings 
            })
            .Where(t => TerrainTypeNames.Contains(t.Name))
            .ToList();

        public List<TileType> CreateTileTypes() => new List<TileType>
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
                    { "MapCharacter", "=" },
                    { "BackgroundColor", ConsoleColor.DarkYellow.ToString() },
                    { "ForegroundColor", ConsoleColor.Yellow.ToString() },
                }
            },
            new TileType
            {
                Name = "Forest",
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
                Name = "Player",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "*" },
                    { "BackgroundColor", ConsoleColor.White.ToString() },
                    { "ForegroundColor", ConsoleColor.Blue.ToString() },
                }
            },
            new TileType
            {
                Name = "Monster",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "!" },
                    { "BackgroundColor", ConsoleColor.Red.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
                }
            },
            new TileType
            {
                Name = "DeadMonster",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "!" },
                    { "BackgroundColor", ConsoleColor.DarkRed.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
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
            }
        };
    }
}
