using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Background service that drives world ticks at the configured rate.
    /// Enumerates active worlds via <see cref="IGameManagementGrain.ListWorldsAsync"/> and
    /// calls <see cref="IWorldGrain.TickAsync"/> on each.
    ///
    /// One tick fan-out runs at <c>SimulationOptions.TickHz</c>. If a tick batch takes longer
    /// than the interval, the next batch starts immediately rather than queuing up — preventing
    /// unbounded backpressure.
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
            var tickInterval = TimeSpan.FromSeconds(1.0 / Math.Max(0.0001, _options.TickHz));
            var managementGrain = _grainFactory.GetGrain<IGameManagementGrain>("GLOBAL");

            while (!stoppingToken.IsCancellationRequested)
            {
                var batchStart = DateTime.UtcNow;

                try
                {
                    var worlds = await managementGrain.ListWorldsAsync();
                    var activeWorldIds = worlds
                        .Where(w => w != null && w.State == WorldState.Active)
                        .Select(w => w.WorldId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();

                    if (activeWorldIds.Count > 0)
                    {
                        var tasks = activeWorldIds
                            .Select(id => SafeTickAsync(id, stoppingToken))
                            .ToArray();
                        await Task.WhenAll(tasks);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WorldTickService] Tick batch failed: {ex.Message}");
                }

                var elapsed = DateTime.UtcNow - batchStart;
                var remaining = tickInterval - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    try { await Task.Delay(remaining, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        /// <summary>
        /// Manually ticks a specific world by ID. Kept for tests and admin endpoints.
        /// </summary>
        public async Task TickWorldAsync(string worldId)
        {
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.TickAsync();
        }

        private async Task SafeTickAsync(string worldId, CancellationToken cancellationToken)
        {
            try
            {
                var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
                await worldGrain.TickAsync();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A single misbehaving world shouldn't poison the whole tick batch.
                Console.WriteLine($"[WorldTickService] Tick failed for world {worldId}: {ex.Message}");
            }
        }
    }
}
