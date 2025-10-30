using System;
using System.Threading.Tasks;
using ConsoleGame.Core;

namespace ConsoleGame
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Client now connects to the server via SignalR
            var game = new ClientConsoleDungeonGame("http://localhost:5000/gamehub");
            await game.Run();
        }
    }
}