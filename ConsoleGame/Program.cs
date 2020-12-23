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
    }
}