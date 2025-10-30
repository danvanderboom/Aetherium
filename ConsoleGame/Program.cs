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

            // UI self-test harness
            if (args.Length > 0 && args[0] == "--ui-selftest")
            {
                var serverUrl = "http://localhost:5000/gamehub";
                string? artifactsDir = null;
                
                if (args.Length > 1)
                {
                    if (args[1].StartsWith("http"))
                    {
                        serverUrl = args[1];
                        if (args.Length > 2) artifactsDir = args[2];
                    }
                    else
                    {
                        artifactsDir = args[1];
                    }
                }

                var selfTest = new ConsoleGame.SelfTest.ConsoleUiSelfTest(serverUrl, artifactsDir);
                try
                {
                    var exitCode = await selfTest.RunMoveDownScenarioAsync();
                    Environment.Exit(exitCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FATAL] Unhandled exception in self-test: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    Environment.Exit(1);
                }
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