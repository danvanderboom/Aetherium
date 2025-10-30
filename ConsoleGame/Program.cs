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
            var game = new ClientConsoleDungeonGame("http://localhost:5000/gamehub");
            await game.Run();
        }
    }
}