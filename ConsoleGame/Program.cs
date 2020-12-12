using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame
{
    class Program
    {
        GameWorld world;

        Character player;

        Character followedMonster1;
        Character followedMonster2;
        Character followedMonster3;
        Character followedMonster4;
        Character followedMonster5;
        Character followedMonster6;
        Character followedMonster7;

        Size3d gameWorldSize = new Size3d(200, 200, 10);
        int monsterCount = 100;

        Size mapSize = new Size(20, 10);
        Point mapLocation = new Point(2, 2);

        ConsoleColor backgroundColor = ConsoleColor.DarkRed;
        ConsoleColor oldBackgroundColor;
        ConsoleColor oldForegroundColor;

        Location playerHomeLocation;

        int interMapDistanceX = 4;
        int interMapDistanceY = 6;

        static void Main(string[] args) => new Program().Run();

        void Run()
        {
            CreateWorld();

            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            ClearScreen(ConsoleColor.Black);

            DrawMapFrames();

            var done = false;
            while (!done)
            {
                DrawMaps();

                Console.CursorVisible = false;

                var characterMoves = world.CharacterMoveTimestamp;

                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(10);

                    if (characterMoves != world.CharacterMoveTimestamp)
                        DrawMaps();
                }

                var keyInfo = Console.ReadKey(true);

                if (!HandleCommand(keyInfo))
                    break;
            }

            Console.BackgroundColor = oldBackgroundColor;
            Console.ForegroundColor = oldForegroundColor;
        }

        void ClearScreen(ConsoleColor? backgroundColor = null)
        {
            if (backgroundColor.HasValue)
            {
                this.backgroundColor = backgroundColor.Value;
                Console.BackgroundColor = backgroundColor.Value;
            }

            Console.Clear();
        }



        private void DrawMapFrames()
        {
            world.DrawMapFrame(mapSize, mapLocation);

            var (x, y) = (mapLocation.X, mapLocation.Y);

            if (followedMonster1 != null)
            {
                x += mapSize.Width + interMapDistanceX;
                world.DrawMapFrame(mapSize, new Point(x, y));

                x += mapSize.Width + interMapDistanceX;
                world.DrawMapFrame(mapSize, new Point(x, y));

                //x += mapSize.Width + interMapDistanceX;
                //world.DrawMapFrame(mapSize, new Point(x, y));

                x = mapLocation.X;
                y += mapSize.Height + interMapDistanceY;
                world.DrawMapFrame(mapSize, new Point(x, y));

                x += mapSize.Width + interMapDistanceX;
                world.DrawMapFrame(mapSize, new Point(x, y));

                x += mapSize.Width + interMapDistanceX;
                world.DrawMapFrame(mapSize, new Point(x, y));

                //x += mapSize.Width + interMapDistanceX;
                //world.DrawMapFrame(mapSize, new Point(x, y));
            }
        }

        void DrawMaps()
        {
            DrawMap(mapLocation, mapSize, player.Get<Location>());

            var (x, y) = (mapLocation.X, mapLocation.Y);

            if (followedMonster1 != null)
            {
                x += mapSize.Width + interMapDistanceX;
                DrawMap(new Point(x, y), mapSize, followedMonster1.Get<Location>());

                x += mapSize.Width + interMapDistanceX;
                DrawMap(new Point(x, y), mapSize, followedMonster2.Get<Location>());

                //x += mapSize.Width + interMapDistanceX;
                //DrawMap(new Point(x, y), mapSize, followedMonster3.Get<Location>());

                x = mapLocation.X;
                y += mapSize.Height + interMapDistanceY;
                DrawMap(new Point(x, y), mapSize, followedMonster4.Get<Location>());

                x += mapSize.Width + interMapDistanceX;
                DrawMap(new Point(x, y), mapSize, followedMonster5.Get<Location>());

                x += mapSize.Width + interMapDistanceX;
                DrawMap(new Point(x, y), mapSize, followedMonster6.Get<Location>());

                //x += mapSize.Width + interMapDistanceX;
                //DrawMap(new Point(x, y), mapSize, followedMonster7.Get<Location>());
            }
        }

        void CreateWorld()
        {
            world = new GameWorld(gameWorldSize.Length, gameWorldSize.Width, gameWorldSize.Depth);
            world.GenerateDefaultTerrain();
            //world.GenerateMazeWorld();

            player = world.AddPlayer("Player 1");

            for (int i = 0; i < monsterCount; i++)
                world.AddMonster("Generic Monster");

            playerHomeLocation = player.Get<Location>();
        }

        void DrawMap(Point mapLocation, Size mapSize, Location location, bool drawFrame = true)
        {
            //if (drawFrame)
            //{
            //    Console.ForegroundColor = ConsoleColor.Cyan;
            //    world.DrawMapFrame(mapSize, mapLocation);
            //}

            mapSize = new Size(mapSize.Width - 2, mapSize.Height - 2);
            mapLocation = new Point(mapLocation.X + 1, mapLocation.Y + 1);

            world.DrawMap(
                mapSize: mapSize,
                locationOnScreenTopLeft: mapLocation,
                locationInWorldTopLeft: new Location(
                    location.X - (mapSize.Width / 2),
                    location.Y - (mapSize.Height / 2),
                    location.Z));

            Console.SetCursorPosition(
                mapLocation.X - 1, // start at the map frame
                mapLocation.Y + mapSize.Height + 2); // map frame + blank line

            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(
                CenterText($"{location.X}, {location.Y}, {location.FromDelta(0, 0, - gameWorldSize.Depth + 1).Z}", 
                mapSize.Width + 2));
        }

        int lockLevel = 0;

        bool HandleCommand(ConsoleKeyInfo keyInfo)
        {
            if (lockLevel > 0)
                lockLevel--;

            switch (keyInfo.Key)
            {
                case ConsoleKey.Backspace:
                    break;
                case ConsoleKey.Tab: // teleport home
                    if (world.TryMove(player, playerHomeLocation))
                        PlayTeleportSound();

                    break;
                case ConsoleKey.Escape:
                    Console.CursorVisible = true;
                    Console.BackgroundColor = oldBackgroundColor;
                    Console.ForegroundColor = oldForegroundColor;
                    return false;
                case ConsoleKey.L:
                    lockLevel = 2;
                    break;
                case ConsoleKey.Spacebar:
                    var z = lockLevel > 0 ? player.Get<Location>().Z : (int?)null;

                    if (world.TryMove(player, world.SelectRandomPassableLocation(zlock: z)))
                        PlayTeleportSound();

                    lockLevel = 0;

                    break;
                case ConsoleKey.N:
                // TODO: set note
                case ConsoleKey.M:
                    if (followedMonster1 == null)
                    {
                        followedMonster1 = world.SelectRandomMonster();
                        followedMonster2 = world.SelectRandomMonster();
                        followedMonster3 = world.SelectRandomMonster();
                        followedMonster4 = world.SelectRandomMonster();
                        followedMonster5 = world.SelectRandomMonster();
                        followedMonster6 = world.SelectRandomMonster();
                        followedMonster7 = world.SelectRandomMonster();

                        DrawMapFrames();
                    }
                    else
                    {
                        followedMonster1 = null;
                        followedMonster2 = null;
                        followedMonster3 = null;
                        followedMonster4 = null;
                        followedMonster5 = null;
                        followedMonster6 = null;
                        followedMonster7 = null;

                        ClearScreen();
                        DrawMapFrames();
                    }

                    break;
                case ConsoleKey.Home: // set teleport home
                    playerHomeLocation = player.Get<Location>();
                    PlaySetTeleportHomeSound();
                    break;
                case ConsoleKey.LeftArrow:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(-1, 0, 0)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.UpArrow:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(0, -1, 0)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.RightArrow:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(+1, 0, 0)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.DownArrow:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(0, +1, 0)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.Enter:
                    var direction = Console.ReadKey();
                    var target = direction.Key switch
                    {
                        ConsoleKey.UpArrow => player.Get<Location>().FromDelta(0, -1, 0),
                        ConsoleKey.DownArrow => player.Get<Location>().FromDelta(0, +1, 0),
                        ConsoleKey.LeftArrow => player.Get<Location>().FromDelta(-1, 0, 0),
                        ConsoleKey.RightArrow => player.Get<Location>().FromDelta(+1, 0, 0),
                        _ => Location.Empty
                    };

                    if (target == Location.Empty)
                        break;

                    if (world.PassableTerrain(target))
                        break;

                    PlayDiggingSound();
                    world.Terrain[target.Z, target.Y, target.X] = TerrainType.Indoors;
                    break;
                case ConsoleKey.U:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(0, 0, +1)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.D:
                    if (!world.TryMove(player, player.Get<Location>().FromDelta(0, 0, -1)))
                        PlayObstructionSound();

                    break;
            }

            return true; // continue game
        }

        static string CenterText(string text, int length)
        {
            var start = (length / 2) - (text.Length / 2);
            return text.PadLeft(text.Length + start).PadRight(length);
        }

        static void PlayTeleportSound()
        {
            Console.Beep(200, 100);
            Console.Beep(400, 100);
            Console.Beep(800, 100);
            Console.Beep(1600, 100);
        }

        private static void PlaySetTeleportHomeSound()
        {
            Console.Beep(1600, 100);
            Console.Beep(200, 100);
        }
        
        private static void PlayObstructionSound()
        {
            Console.Beep(200, 100);
        }

        private void PlayDiggingSound()
        {
            Console.Beep(300, 25);
        }

        static void Write(string text, Point location, 
            ConsoleColor foregroundColor = ConsoleColor.Cyan, 
            ConsoleColor backgroundColor = ConsoleColor.Black)
        {
            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = foregroundColor;

            Console.SetCursorPosition(location.X, location.Y);
            Console.Write(text);
        }
    }
}