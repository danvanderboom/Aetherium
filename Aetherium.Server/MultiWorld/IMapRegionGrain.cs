using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Persistence;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain interface for managing a region (64×64 chunk) of a map.
    /// Each region owns its dynamic state: entities, terrain modifications, heatmaps, etc.
    /// </summary>
    public interface IMapRegionGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the region with its bounds and map reference.
        /// </summary>
        Task InitializeAsync(string mapId, int regionX, int regionY, int zLevel, int regionSize);

        /// <summary>
        /// Processes a simulation tick for this region.
        /// </summary>
        Task TickAsync(TimeSpan gameTimeElapsed);

        /// <summary>
        /// Gets the current snapshot of this region's state.
        /// </summary>
        Task<RegionStateSnapshot> GetSnapshotAsync();

        /// <summary>
        /// Loads a snapshot to restore region state.
        /// </summary>
        Task LoadSnapshotAsync(RegionStateSnapshot snapshot);

        /// <summary>
        /// Applies a delta to the region state (for incremental updates).
        /// </summary>
        Task ApplyDeltaAsync(RegionDelta delta);

        /// <summary>
        /// Records entity traversal at a location (for path emergence).
        /// </summary>
        Task RecordTraversalAsync(int x, int y);

        /// <summary>
        /// Gets traversal heatmap data for this region.
        /// </summary>
        Task<Dictionary<(int x, int y), int>> GetTraversalHeatmapAsync();
    }
}

