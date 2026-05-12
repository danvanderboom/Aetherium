using System;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// Interface for storing and retrieving world region snapshots, plus the
    /// append-only delta log that bridges snapshots.
    /// </summary>
    /// <remarks>
    /// Two delta APIs coexist:
    /// <list type="bullet">
    ///   <item><description>The legacy <see cref="RegionDelta"/> API (timestamp-keyed, dict-bag payload) for compatibility with early-phase tests and tooling.</description></item>
    ///   <item><description>The production <see cref="MapDelta"/> API (sequence-keyed, full <c>[GenerateSerializer]</c> wire payload) used by <c>GameMapGrain.FanOutAsync</c>.</description></item>
    /// </list>
    /// </remarks>
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
        /// Appends a legacy timestamp-keyed delta to the delta log for a region.
        /// </summary>
        Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta);

        /// <summary>
        /// Gets legacy deltas for a region since a given timestamp.
        /// </summary>
        Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since);

        /// <summary>
        /// Compacts a region's legacy delta log into a new snapshot.
        /// </summary>
        Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot);

        /// <summary>
        /// Appends a wire <see cref="MapDelta"/> to the sequence-keyed log for a region. The delta's
        /// <see cref="MapDelta.Sequence"/> is the primary key — the caller must have stamped it
        /// before calling. Implementations SHOULD reject duplicate sequence values for the same
        /// (worldId, regionId) pair.
        /// </summary>
        Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta);

        /// <summary>
        /// Returns map deltas with <c>Sequence &gt; sinceSequence</c>, in ascending sequence order.
        /// Used on grain activation to replay the log atop a loaded snapshot.
        /// </summary>
        Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence);

        /// <summary>
        /// Deletes map deltas with <c>Sequence &lt;= throughSequence</c>. Called by the compaction
        /// timer once a new snapshot at that sequence is durable.
        /// </summary>
        Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence);
    }
}

