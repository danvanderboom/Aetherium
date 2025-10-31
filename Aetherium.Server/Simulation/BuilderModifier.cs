using System;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Temporal modifier that applies BuilderAI to regions during ticks.
    /// NOTE: Currently requires World access which is not available from IMapRegionGrain.
    /// This is a placeholder implementation for future integration.
    /// </summary>
    public class BuilderModifier : ITemporalModifier
    {
        private readonly BuilderAI _builderAI;
        private readonly Func<IMapRegionGrain, Task<World?>>? _worldProvider;

        public string Name => "builder";
        public int Priority => 100; // Lower priority - runs after other modifiers

        public BuilderModifier(BuilderAI builderAI, Func<IMapRegionGrain, Task<World?>>? worldProvider = null)
        {
            _builderAI = builderAI ?? throw new ArgumentNullException(nameof(builderAI));
            _worldProvider = worldProvider;
        }

        public async Task ApplyAsync(
            IMapRegionGrain region,
            RegionStateSnapshot regionSnapshot,
            System.TimeSpan gameTimeElapsed,
            double timeOfDay,
            int day)
        {
            // Get world reference if provider is available
            World? world = null;
            if (_worldProvider != null)
            {
                world = await _worldProvider(region);
            }

            if (world == null)
            {
                // World access not available - skip building for now
                // TODO: Integrate with GameMapGrain to get World reference
                return;
            }

            // Process build tasks
            await _builderAI.ProcessBuildTasksAsync(region, regionSnapshot, world, timeOfDay, day);
        }
    }
}

