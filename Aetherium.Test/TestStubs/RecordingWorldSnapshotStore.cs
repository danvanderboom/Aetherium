using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.TestStubs
{
    /// <summary>
    /// In-memory <see cref="IWorldSnapshotStore"/> that records every <see cref="MapDelta"/>
    /// appended via <see cref="AppendMapDeltaAsync"/> into a thread-safe list so tests can
    /// assert what the grain fan-out path actually persisted. All other methods delegate
    /// to a wrapped <see cref="InMemoryWorldSnapshotStore"/>.
    /// </summary>
    internal sealed class RecordingWorldSnapshotStore : IWorldSnapshotStore
    {
        private readonly InMemoryWorldSnapshotStore _inner = new();
        private readonly ConcurrentBag<(string WorldId, string RegionId, MapDelta Delta)> _records = new();

        public IReadOnlyList<MapDelta> Recorded =>
            _records.OrderBy(r => r.Delta.Sequence).Select(r => r.Delta).ToArray();

        public IReadOnlyList<(string WorldId, string RegionId, MapDelta Delta)> RecordedFull =>
            _records.OrderBy(r => r.Delta.Sequence).ToArray();

        public void Reset() { while (_records.TryTake(out _)) { } }

        public Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot) =>
            _inner.SaveSnapshotAsync(worldId, snapshot);
        public Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId) =>
            _inner.LoadSnapshotAsync(worldId, regionId);
        public Task DeleteSnapshotAsync(string worldId, string regionId) =>
            _inner.DeleteSnapshotAsync(worldId, regionId);
        public Task<string[]> ListRegionIdsAsync(string worldId) =>
            _inner.ListRegionIdsAsync(worldId);
        public Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta) =>
            _inner.AppendDeltaAsync(worldId, regionId, delta);
        public Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since) =>
            _inner.GetDeltasSinceAsync(worldId, regionId, since);
        public Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot) =>
            _inner.CompactDeltasAsync(worldId, regionId, baseSnapshot);

        public Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta)
        {
            _records.Add((worldId, regionId, delta));
            return _inner.AppendMapDeltaAsync(worldId, regionId, delta);
        }
        public Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence) =>
            _inner.GetMapDeltasSinceSequenceAsync(worldId, regionId, sinceSequence);
        public Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence) =>
            _inner.CompactMapDeltasAsync(worldId, regionId, throughSequence);
        public Task DeleteWorldAsync(string worldId) =>
            _inner.DeleteWorldAsync(worldId);
    }
}
