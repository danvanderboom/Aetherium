using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame
{
    class Program
    {
        GameWorld world;
        Character player;
        Character followedPlayer;
        bool followMonsterMode = false;

        Size3d gameWorldSize = new Size3d(500, 500, 1);
        int monsterCount = 1000;

        Size mapSize = new Size(60, 20);
        Point mapLocationOnScreenTopLeft = new Point(4, 3);

        ConsoleColor backgroundColor = ConsoleColor.Black;
        ConsoleColor oldBackgroundColor;
        ConsoleColor oldForegroundColor;

        Location playerHomeLocation;

        static void Main(string[] args) => new Program().Run();

        void Run()
        {
            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            CreateWorld();
            DrawLayout();

            var done = false;
            while (!done)
            {
                DrawMap();

                Console.CursorVisible = false;

                var monsterMoveCount = world.MonsterMoveTimestamp;

                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(10);

                    if (monsterMoveCount != world.MonsterMoveTimestamp)
                        DrawMap();
                }

                var keyInfo = Console.ReadKey(true);

                HandleCommand(keyInfo);
            }

            Console.BackgroundColor = oldBackgroundColor;
            Console.ForegroundColor = oldForegroundColor;
        }

        void CreateWorld()
        {
            world = new GameWorld(gameWorldSize.Length, gameWorldSize.Width, gameWorldSize.Depth);
            world.GenerateDefaultTerrain();
            //world.GenerateMazeWorld();

            player = world.AddPlayer("Player 1");
            followedPlayer = player;

            for (int i = 0; i < monsterCount; i++)
                world.AddMonster("Generic Monster");

            playerHomeLocation = player.Get<Location>();
        }

        void DrawLayout()
        {
            backgroundColor = ConsoleColor.Black;
            Console.BackgroundColor = backgroundColor;
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Cyan;
            world.DrawMapFrame(mapSize, mapLocationOnScreenTopLeft);
        }

        void DrawMap()
        {
            var location = followedPlayer.Get<Location>();

            world.DrawMap(
                mapSize: mapSize,
                locationOnScreenTopLeft: mapLocationOnScreenTopLeft,
                locationInWorldTopLeft: new Location(
                    location.X - (mapSize.Width / 2),
                    location.Y - (mapSize.Height / 2),
                    location.Z));

            Console.SetCursorPosition(
                mapLocationOnScreenTopLeft.X - 1, // start at the map frame
                mapLocationOnScreenTopLeft.Y + mapSize.Height + 2); // map frame + blank line

            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(
                CenterText($"{location.X}, {location.Y}, {location.Z}", 
                mapSize.Width + 2));
        }

        void HandleCommand(ConsoleKeyInfo keyInfo)
        {
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
                    Environment.Exit(0);
                    break;
                case ConsoleKey.Spacebar:
                    if (world.TryMove(player, world.SelectRandomPassableLocation()))
                        PlayTeleportSound();

                    break;
                case ConsoleKey.N:
                // TODO: set note
                case ConsoleKey.M:
                    followMonsterMode = !followMonsterMode;

                    if (followMonsterMode)
                        followedPlayer = world.SelectRandomMonster() ?? player;
                    else
                        followedPlayer = player;

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
            }
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