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
        //static void Main() => new ConsoleDungeonGame(new TorusWorldBuilder()).Run();
        
        // FOV Diagnostic Test Maps - uncomment the one you want to test:
        //static void Main() => new ConsoleDungeonGame(new TestMazeWorldBuilder()).Run();
        
        // Start in a simple open space scenario with good visibility
        static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("open_space")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("simple_wall")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("corner_occlusion")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("partial_opacity")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("door_test")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("multiwall")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("diagonal_wall")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("cross_hair")).Run();
        //static void Main() => new ConsoleDungeonGame(new FovDiagnosticWorldBuilder("chamber")).Run();
    }
}