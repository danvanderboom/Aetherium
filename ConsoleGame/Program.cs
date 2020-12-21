using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame
{
    class Program
    {
        //static void Main() => new ConsoleDungeonGame(new DungeonCrawlerWorldBuilder()).Run();
        static void Main() => new ConsoleDungeonGame(new TorusWorldBuilder()).Run();

        //void Run()
        //{
        //    CreateWorld();

        //    oldBackgroundColor = Console.BackgroundColor;
        //    oldForegroundColor = Console.ForegroundColor;

        //    ClearScreen(ConsoleColor.Black);

        //    DrawMapFrames();

        //    var done = false;
        //    while (!done)
        //    {
        //        DrawMaps();

        //        Console.CursorVisible = false;

        //        var characterMoves = world.CharacterMoveTimestamp;

        //        while (!Console.KeyAvailable)
        //        {
        //            Thread.Sleep(10);

        //            if (characterMoves != world.CharacterMoveTimestamp)
        //                DrawMaps();
        //        }

        //        var keyInfo = Console.ReadKey(true);

        //        if (!HandleCommand(keyInfo))
        //            break;
        //    }

        //    Console.BackgroundColor = oldBackgroundColor;
        //    Console.ForegroundColor = oldForegroundColor;
        //}

        //void CreateWorld()
        //{
        //    world = new GameWorld(gameWorldSize.Length, gameWorldSize.Width, gameWorldSize.Depth);
        //    world.GenerateDefaultTerrain();
        //    //world.GenerateMazeWorld();

        //    world.CharacterDied += World_CharacterDied;

        //    player = world.AddPlayer("Player 1");

        //    for (int i = 0; i < monsterCount; i++)
        //        world.AddMonster("Generic Monster");

        //    playerHomeLocation = player.Get<Location>();
        //}

        //bool HandleCommand(ConsoleKeyInfo keyInfo)
        //{
        //        case ConsoleKey.M:
        //            if (followedMonster1 == null)
        //            {
        //                followedMonster1 = world.SelectRandomMonster();
        //                followedMonster2 = world.SelectRandomMonster();
        //                followedMonster3 = world.SelectRandomMonster();
        //                followedMonster4 = world.SelectRandomMonster();
        //                followedMonster5 = world.SelectRandomMonster();
        //                followedMonster6 = world.SelectRandomMonster();
        //                followedMonster7 = world.SelectRandomMonster();

        //                DrawMapFrames();
        //            }
        //            else
        //            {
        //                followedMonster1 = null;
        //                followedMonster2 = null;
        //                followedMonster3 = null;
        //                followedMonster4 = null;
        //                followedMonster5 = null;
        //                followedMonster6 = null;
        //                followedMonster7 = null;

        //                ClearScreen();
        //                DrawMapFrames();
        //            }

        //            break;
        //        case ConsoleKey.Enter:
        //            var direction = Console.ReadKey(true);
        //            var target = direction.Key switch
        //            {
        //                ConsoleKey.UpArrow => player.Get<Location>().FromDelta(0, -1, 0),
        //                ConsoleKey.DownArrow => player.Get<Location>().FromDelta(0, +1, 0),
        //                ConsoleKey.LeftArrow => player.Get<Location>().FromDelta(-1, 0, 0),
        //                ConsoleKey.RightArrow => player.Get<Location>().FromDelta(+1, 0, 0),
        //                ConsoleKey.U => player.Get<Location>().FromDelta(0, 0, +1),
        //                ConsoleKey.D => player.Get<Location>().FromDelta(0, 0, -1),
        //                _ => Location.Empty
        //            };

        //            if (target == Location.Empty)
        //                break;

        //            if (!world.PassableTerrain(target))
        //                break;

        //            SoundEffects.PlayDiggingSound();

        //            if (direction.Key != ConsoleKey.U && direction.Key != ConsoleKey.D)
        //            {
        //                world.SetTerrain(target, TerrainType.Indoors);
        //            }
        //            else
        //            {
        //                if (direction.Key == ConsoleKey.U)
        //                {
        //                    world.SetTerrain(player.Get<Location>(), TerrainType.Upstairs);
        //                    world.SetTerrain(player.Get<Location>().FromDelta(0, 0, +1), TerrainType.Downstairs);
        //                }
        //                else
        //                {
        //                    world.SetTerrain(player.Get<Location>(), TerrainType.Downstairs);
        //                    world.SetTerrain(player.Get<Location>().FromDelta(0, 0, -1), TerrainType.Upstairs);
        //                }
        //            }

        //            break;
        //    }

        //    return true; // continue game
        //}
    }
}