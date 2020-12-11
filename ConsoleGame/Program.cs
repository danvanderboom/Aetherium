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

        Size gameWorldSize = new Size(500, 500);
        int monsterCount = 1000;

        Size mapSize = new Size(60, 20);
        Point mapLocationOnScreenTopLeft = new Point(4, 3);

        ConsoleColor backgroundColor = ConsoleColor.Black;
        ConsoleColor oldBackgroundColor;
        ConsoleColor oldForegroundColor;

        Position playerHomeLocation;

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
            world = new GameWorld(gameWorldSize.Width, gameWorldSize.Height, 1);
            world.GenerateDefaultTerrain();
            //world.GenerateMazeWorld();

            player = world.AddPlayer("Player 1");
            followedPlayer = player;

            for (int i = 0; i < monsterCount; i++)
                world.AddMonster("Generic Monster");

            playerHomeLocation = player.Location;
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
            world.DrawMap(
                mapSize: mapSize,
                locationOnScreenTopLeft: mapLocationOnScreenTopLeft,
                locationInWorldTopLeft: new Position(
                    followedPlayer.Location.X - (mapSize.Width / 2),
                    followedPlayer.Location.Y - (mapSize.Height / 2),
                    z: 0));

            Console.SetCursorPosition(
                mapLocationOnScreenTopLeft.X - 1, // start at the map frame
                mapLocationOnScreenTopLeft.Y + mapSize.Height + 2); // map frame + blank line

            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(CenterText($"{followedPlayer.Location.X}, {followedPlayer.Location.Y}", mapSize.Width + 2));
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
                    playerHomeLocation = player.Location;
                    PlaySetTeleportHomeSound();
                    break;
                case ConsoleKey.LeftArrow:
                    if (!world.TryMove(player, new Position(player.Location.X - 1, player.Location.Y, player.Location.Z)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.UpArrow:
                    if (!world.TryMove(player, new Position(player.Location.X, player.Location.Y - 1, player.Location.Z)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.RightArrow:
                    if (!world.TryMove(player, new Position(player.Location.X + 1, player.Location.Y, player.Location.Z)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.DownArrow:
                    if (!world.TryMove(player, new Position(player.Location.X, player.Location.Y + 1, player.Location.Z)))
                        PlayObstructionSound();

                    break;
                case ConsoleKey.Enter:
                    var direction = Console.ReadKey();
                    var target = direction.Key switch
                    {
                        ConsoleKey.UpArrow => new Position(player.Location.X, player.Location.Y - 1, player.Location.Z),
                        ConsoleKey.DownArrow => new Position(player.Location.X, player.Location.Y + 1, player.Location.Z),
                        ConsoleKey.LeftArrow => new Position(player.Location.X - 1, player.Location.Y, player.Location.Z),
                        ConsoleKey.RightArrow => new Position(player.Location.X + 1, player.Location.Y, player.Location.Z),
                        _ => Position.Empty
                    };

                    if (target == Position.Empty)
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