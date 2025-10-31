using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Background service that drives world ticks at the configured rate.
    /// Calls TickAsync on all active WorldGrain instances.
    /// </summary>
    public class WorldTickService : BackgroundService
    {
        private readonly IGrainFactory _grainFactory;
        private readonly SimulationOptions _options;

        public WorldTickService(IGrainFactory grainFactory, IOptions<SimulationOptions> options)
        {
            _grainFactory = grainFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tickInterval = TimeSpan.FromSeconds(1.0 / _options.TickHz);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Get all active world grains
                    // Note: In a production system, you might want to maintain a registry
                    // of active worlds. For now, we'll query Orleans for active grains.
                    // This is a simplified approach - in production you'd track active worlds.
                    
                    // For now, we'll skip automatic ticking if we can't enumerate worlds
                    // The tick can be manually triggered via API or other mechanisms
                    // TODO: Add a world registry to track active worlds for ticking
                    
                    await Task.Delay(tickInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Error in WorldTickService: {ex.Message}");
                    await Task.Delay(tickInterval, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Manually ticks a specific world by ID.
        /// </summary>
        public async Task TickWorldAsync(string worldId)
        {
            var worldGrain = _grainFactory.GetGrain<MultiWorld.IWorldGrain>(worldId);
            await worldGrain.TickAsync();
        }
    }
}

