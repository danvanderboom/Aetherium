using System;
using System.Threading.Tasks;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// Interface for storing and retrieving world region snapshots.
    /// </summary>
    public interface IWorldSnapshotStore
    {
        /// <summary>
        /// Saves a region snapshot.
        /// </summary>
        Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot);

        /// <summary>
        /// Loads a region snapshot.
        /// </summary>
        Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId);

        /// <summary>
        /// Deletes a region snapshot.
        /// </summary>
        Task DeleteSnapshotAsync(string worldId, string regionId);

        /// <summary>
        /// Lists all region IDs for a world.
        /// </summary>
        Task<string[]> ListRegionIdsAsync(string worldId);

        /// <summary>
        /// Appends a delta to the delta log for a region.
        /// </summary>
        Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta);

        /// <summary>
        /// Gets deltas for a region since a given timestamp.
        /// </summary>
        Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since);

        /// <summary>
        /// Compacts a region's delta log into a new snapshot.
        /// </summary>
        Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot);
    }
}

