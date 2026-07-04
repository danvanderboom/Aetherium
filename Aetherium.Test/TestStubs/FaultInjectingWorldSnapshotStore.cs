using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.TestStubs
{
    /// <summary>
    /// In-memory snapshot store whose <see cref="AppendMapDeltaAsync"/> can be flipped to throw,
    /// so tests can exercise GameMapGrain's delta-append failure handling (P3-8): failures must be
    /// recorded (not swallowed), and a snapshot must be captured once persistence recovers.
    /// </summary>
    public sealed class FaultInjectingWorldSnapshotStore : IWorldSnapshotStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RegionStateSnapshot>> _snapshots = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<MapDelta>>> _mapDeltas = new();

        /// <summary>When true, <see cref="AppendMapDeltaAsync"/> throws instead of appending.</summary>
        public volatile bool FailAppends;

        private int _saveSnapshotCount;
        /// <summary>Number of times <see cref="SaveSnapshotAsync"/> has been called (heal-snapshot observability).</summary>
        public int SaveSnapshotCount => Volatile.Read(ref _saveSnapshotCount);

        public Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot)
        {
            Interlocked.Increment(ref _saveSnapshotCount);
            var regions = _snapshots.GetOrAdd(worldId, _ => new());
            regions[snapshot.RegionId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId)
        {
            if (_snapshots.TryGetValue(worldId, out var regions) && regions.TryGetValue(regionId, out var snap))
                return Task.FromResult<RegionStateSnapshot?>(snap);
            return Task.FromResult<RegionStateSnapshot?>(null);
        }

        public Task DeleteSnapshotAsync(string worldId, string regionId)
        {
            if (_snapshots.TryGetValue(worldId, out var regions)) regions.TryRemove(regionId, out _);
            return Task.CompletedTask;
        }

        public Task<string[]> ListRegionIdsAsync(string worldId)
            => Task.FromResult(_snapshots.TryGetValue(worldId, out var regions) ? regions.Keys.ToArray() : Array.Empty<string>());

        public Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta) => Task.CompletedTask;

        public Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since)
            => Task.FromResult(Array.Empty<RegionDelta>());

        public Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot) => Task.CompletedTask;

        public Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta)
        {
            if (FailAppends)
                throw new InvalidOperationException("Injected append failure (test)");

            var regions = _mapDeltas.GetOrAdd(worldId, _ => new());
            var list = regions.GetOrAdd(regionId, _ => new());
            lock (list) { list.Add(delta); }
            return Task.CompletedTask;
        }

        public Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence)
        {
            if (_mapDeltas.TryGetValue(worldId, out var regions) && regions.TryGetValue(regionId, out var list))
                lock (list) { return Task.FromResult(list.Where(d => d.Sequence > sinceSequence).OrderBy(d => d.Sequence).ToArray()); }
            return Task.FromResult(Array.Empty<MapDelta>());
        }

        public Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence)
        {
            if (_mapDeltas.TryGetValue(worldId, out var regions) && regions.TryGetValue(regionId, out var list))
                lock (list) { list.RemoveAll(d => d.Sequence <= throughSequence); }
            return Task.CompletedTask;
        }

        public Task DeleteWorldAsync(string worldId)
        {
            _snapshots.TryRemove(worldId, out _);
            _mapDeltas.TryRemove(worldId, out _);
            return Task.CompletedTask;
        }
    }
}
