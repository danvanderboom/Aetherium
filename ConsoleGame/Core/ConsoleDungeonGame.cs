using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;
using ConsoleGame.Views;
using ConsoleGame.Entities;

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

        Guid characterMoves = Guid.Empty;

        bool followMazeBuilder = false;

        Task? MonsterHeartbeatTask;

        Random rand = new Random();

        public ConsoleDungeonGame(WorldBuilder worldBuilder) 
        {
            this.worldBuilder = worldBuilder;

            World = worldBuilder.Build();

            var torusBuilder = worldBuilder as TorusWorldBuilder;
            if (torusBuilder != null)
                torusBuilder.MazeLocationSet += ConsoleDungeonGame_MazeLocationSet;

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

        private void ConsoleDungeonGame_MazeLocationSet(WorldLocation location)
        {
            if (followMazeBuilder)
                mapView.WorldLocation = location;
        }

        public void Run()
        {
            //AddPlayerCharacter();
            //AddMonsters(100);

            MonsterHeartbeatTask = Task.Run(MonsterHeartbeat);

            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            try
            {
                if (!Console.IsOutputRedirected)
                    Console.CursorVisible = false;
            }
            catch (System.IO.IOException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
            Console.OutputEncoding = Encoding.Unicode;

            Clear(ConsoleColor.Black);

            DisplayViews();

            bool continueMaze = true;
            int mazeSteps = 0;

            while (true)
            {
                while (!Console.KeyAvailable)
                {
                    if (continueMaze)
                    {
                        var torusBuilder = worldBuilder as TorusWorldBuilder;
                        if (torusBuilder != null && continueMaze)
                        {
                            continueMaze = torusBuilder.BuildMazeStep();

                            // double step
                            if (continueMaze)
                                continueMaze = torusBuilder.BuildMazeStep();

                            // triple step
                            if (continueMaze)
                                continueMaze = torusBuilder.BuildMazeStep();

                            mazeSteps++;
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }

                    //if (characterMoves != World.CharacterMoveTimestamp)
                    {
                        DisplayViewContents();
                        //characterMoves = World.CharacterMoveTimestamp;
                    }
                }

                var keyInfo = Console.ReadKey(true);
                HandleCommand(keyInfo);

                DisplayViewContents();
            }
        }

        void AddPlayerCharacter()
        {
            var location = World.SelectRandomPassableLocation();
            if (location != null)
            {
                var player = new Character();
                player.Set(new Health { Level = 100, MaxLevel = 100 });
                player.Set(new HasHeading { Heading = rand.Next(0, 4) * 90 });
                player.Set(new Tile { Type = World.TileTypes["Player"] });
                player.Set(location);

                World.AddEntity(player);
            }
        }

        void AddMonsters(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var location = World.SelectRandomPassableLocation();
                if (location != null && location.Z != -1)
                {
                    var zombie = new Zombie(World);
                    zombie.Set(new Health { Level = 10, MaxLevel = 10 });
                    zombie.Set(new HasHeading { Heading = rand.Next(0, 4) * 90 });
                    zombie.Set(location);

                    World.AddEntity(zombie);
                }
            }
        }

        async Task MonsterHeartbeat()
        {
            while (true)
            {
                try
                {
                    var characters = World.Characters.Values.ToList();

                    foreach (Character character in characters)
                        if (character is Monster monster && monster.Get<Health>().Level > 0)
                            monster.Heartbeat();

                    await Task.Delay(20);
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void HandleCommand(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Spacebar:
                    followMazeBuilder = !followMazeBuilder;
                    break;
                case ConsoleKey.D0: // digit zero
                    if (mapView?.WorldLocation != null)
                    {
                        var z = mapView.WorldLocation.Z;
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, -z);
                        followMazeBuilder = false;
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
                    {
                        mapView.Move(RelativeDirection.Forward, Console.CapsLock ? 10 : 1);
                        followMazeBuilder = false;
                    }
                    break;
                case ConsoleKey.DownArrow:
                    if (mapView?.WorldLocation != null)
                    {
                        mapView.Move(RelativeDirection.Backward, Console.CapsLock ? 10 : 1);
                        followMazeBuilder = false;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                    if (mapView?.WorldLocation != null)
                    {
                        mapView.Move(RelativeDirection.Left, Console.CapsLock ? 10 : 1);
                        followMazeBuilder = false;
                    }
                    break;
                case ConsoleKey.RightArrow:
                    if (mapView?.WorldLocation != null)
                    {
                        mapView.Move(RelativeDirection.Right, Console.CapsLock ? 10 : 1);
                        followMazeBuilder = false;
                    }
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
                    {
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, Console.CapsLock ? +10 : +1);
                        followMazeBuilder = false;
                    }
                    break;
                case ConsoleKey.D:
                    if (mapView?.WorldLocation != null)
                    {
                        mapView.WorldLocation = mapView.WorldLocation.FromDelta(0, 0, Console.CapsLock ? -10 : -1);
                        followMazeBuilder = false;
                    }
                    break;
                case ConsoleKey.C:
                    if (mapView != null)
                        Clear(mapView.ContentScreenPosition, mapView.ContentSize, ConsoleColor.DarkYellow);
                    break;
                case ConsoleKey.J:
                    var locationCount = World.EntitiesByLocation.Keys.Count;
                    if (mapView == null || locationCount == 0)
                        break;

                    var location = World.EntitiesByLocation.Keys
                        .Skip(rand.Next(0, locationCount))
                        .First();

                    mapView.WorldLocation = location;

                    followMazeBuilder = false;
                    break;
            }
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
