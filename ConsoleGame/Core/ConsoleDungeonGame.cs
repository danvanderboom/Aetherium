using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame.Views;

namespace ConsoleGame.Core
{
    public class ConsoleDungeonGame
    {
        public World World { get; protected set; }

        public Character? player { get; protected set; }

        WorldBuilder worldBuilder;

        List<ConsoleView> Views;

        ConsoleMapView mapView;

        ConsoleColor oldBackgroundColor;
        ConsoleColor oldForegroundColor;

        Random rand = new Random();

        public ConsoleDungeonGame(WorldBuilder worldBuilder) 
        {
            this.worldBuilder = worldBuilder;

            World = worldBuilder.Build();

            Views = new List<ConsoleView>();

            mapView = new ConsoleMapView
            {
                ScreenPosition = new Point(3, 2),
                Size = new Size(42, 22),
                BackgroundColor = ConsoleColor.Black,
                HasFrame = true,
                FrameBackgroundColor = ConsoleColor.DarkGray,
                FrameForegroundColor = ConsoleColor.Black,
                World = this.World,
                TileTypes = World.TileTypes.Values.ToList()
            };
            
            Views.Add(mapView);

            var locations = World.EntitiesByLocation.Keys.ToList();
            if (locations.Any())
            {
                var randomLocation = locations[rand.Next(0, locations.Count)];
                mapView.WorldLocation = randomLocation;
            }
        }

        public void Run()
        {
            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            Console.CursorVisible = false;
            Console.OutputEncoding = Encoding.Unicode;

            Clear(ConsoleColor.Black);

            DisplayViews();

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.D0:
                        if (mapView?.WorldLocation != null)
                        {
                            var z = mapView.WorldLocation.Z;
                            mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, -z);
                        }
                        break;
                    case ConsoleKey.M:
                        if (mapView?.WorldLocation != null)
                        {
                            if (mapView.GridColoring == null)
                            {
                                var choice = Console.ReadKey(true).KeyChar.ToString();
                                if (int.TryParse(choice, out var gridIndex))
                                {
                                    if (gridIndex == 0)
                                        mapView.GridColoring = new ConsoleColor[2, 2]
                                        {
                                            { ConsoleColor.Red, ConsoleColor.Blue },
                                            { ConsoleColor.Blue, ConsoleColor.White }
                                        };
                                    else if (gridIndex == 1)
                                        mapView.GridColoring = new ConsoleColor[3, 3]
                                        {
                                            { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Yellow },
                                            { ConsoleColor.Blue, ConsoleColor.Blue, ConsoleColor.Yellow },
                                            { ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Blue }
                                        };
                                    else if (gridIndex == 2)
                                        mapView.GridColoring = new ConsoleColor[3, 3]
                                        {
                                            { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Yellow },
                                            { ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Yellow },
                                            { ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Cyan }
                                        };
                                }
                            }
                            else
                            {
                                mapView.GridColoring = null;
                            }
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        if (mapView?.WorldLocation != null)
                            mapView.Move(RelativeDirection.Up, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.DownArrow:
                        if (mapView?.WorldLocation != null)
                            mapView.Move(RelativeDirection.Down, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.LeftArrow:
                        if (mapView?.WorldLocation != null)
                            mapView.Move(RelativeDirection.Left, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.RightArrow:
                        if (mapView?.WorldLocation != null)
                            mapView.Move(RelativeDirection.Right, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.Z:
                        if (mapView?.WorldLocation != null)
                            mapView.RotateLeft();
                        break;
                    case ConsoleKey.X:
                        if (mapView?.WorldLocation != null)
                            mapView.RotateRight();
                        break;
                    case ConsoleKey.U:
                        if (mapView?.WorldLocation != null)
                            mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, Console.CapsLock ? +10 : +1);
                        break;
                    case ConsoleKey.D:
                        if (mapView?.WorldLocation != null)
                            mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, Console.CapsLock ? -10 : -1);
                        break;
                    case ConsoleKey.C:
                        if (mapView != null)
                            Clear(mapView.ContentScreenPosition, mapView.ContentSize, ConsoleColor.DarkYellow);
                        continue;
                    case ConsoleKey.J:
                        var locationCount = World.EntitiesByLocation.Keys.Count;
                        if (mapView == null || locationCount == 0)
                            continue;

                        var location = World.EntitiesByLocation.Keys
                            .Skip(rand.Next(0, locationCount))
                            .First();

                        mapView.WorldLocation = location;
                        break;
                }

                DisplayViewContents();
            }

            //Console.CursorVisible = false;
            //Console.BackgroundColor = oldBackgroundColor;
            //Console.ForegroundColor = oldForegroundColor;
        }

        public void DisplayViews()
        {
            foreach (var view in Views)
            {
                if (view.HasFrame)
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
    }
}
