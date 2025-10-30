using System;
using System.Threading.Tasks;
using ConsoleGame.Core;
using ConsoleGame.Monitoring;

namespace ConsoleGame
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try { Console.Title = "ConsoleGameClient"; } catch {}
            // Check for audio test argument
            if (args.Length > 0 && args[0] == "--audio-test")
            {
                Console.WriteLine("Audio test functionality has been removed. Audio is now integrated into the main game.");
                Console.WriteLine("Run the game normally to hear background music and sound effects.");
                return;
            }

            // Initialize monitoring service
            var monitoringConfig = new MonitoringConfig
            {
                Enabled = true,
                Port = 5001,
                FileLogging = new FileLoggingConfig
                {
                    Enabled = false, // Set to true to enable file logging
                    OutputPath = "./monitoring-logs"
                }
            };

            MapFrameMonitor.Initialize(monitoringConfig);

            // Start the monitoring service
            if (monitoringConfig.Enabled)
            {
                await MapFrameMonitor.Instance.StartAsync();
                Console.WriteLine("[Monitor] Service started. Monitoring endpoint available at ws://localhost:5001/monitor");
            }

            // Client now connects to the server via SignalR
            var game = new ClientConsoleDungeonGameNew("http://localhost:5000/gamehub");
            await game.Run();
        }
    }
}