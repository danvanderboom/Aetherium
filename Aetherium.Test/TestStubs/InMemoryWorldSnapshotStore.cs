using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;

namespace Aetherium.Test.TestStubs
{
    internal sealed class InMemoryWorldSnapshotStore : IWorldSnapshotStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RegionStateSnapshot>> _worldIdToRegionIdToSnapshot = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<RegionDelta>>> _worldIdToRegionIdToDeltas = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<MapDelta>>> _worldIdToRegionIdToMapDeltas = new();

        public Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot)
        {
            var regions = _worldIdToRegionIdToSnapshot.GetOrAdd(worldId, _ => new());
            regions[snapshot.RegionId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId)
        {
            if (_worldIdToRegionIdToSnapshot.TryGetValue(worldId, out var regions) &&
                regions.TryGetValue(regionId, out var snapshot))
            {
                return Task.FromResult<RegionStateSnapshot?>(snapshot);
            }
            return Task.FromResult<RegionStateSnapshot?>(null);
        }

        public Task DeleteSnapshotAsync(string worldId, string regionId)
        {
            if (_worldIdToRegionIdToSnapshot.TryGetValue(worldId, out var regions))
            {
                regions.TryRemove(regionId, out _);
            }
            if (_worldIdToRegionIdToDeltas.TryGetValue(worldId, out var deltas))
            {
                deltas.TryRemove(regionId, out _);
            }
            return Task.CompletedTask;
        }

        public Task<string[]> ListRegionIdsAsync(string worldId)
        {
            if (_worldIdToRegionIdToSnapshot.TryGetValue(worldId, out var regions))
            {
                return Task.FromResult(regions.Keys.ToArray());
            }
            return Task.FromResult(Array.Empty<string>());
        }

        public Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta)
        {
            var regions = _worldIdToRegionIdToDeltas.GetOrAdd(worldId, _ => new());
            var list = regions.GetOrAdd(regionId, _ => new());
            lock (list)
            {
                list.Add(delta);
            }
            return Task.CompletedTask;
        }

        public Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since)
        {
            if (_worldIdToRegionIdToDeltas.TryGetValue(worldId, out var regions) &&
                regions.TryGetValue(regionId, out var list))
            {
                lock (list)
                {
                    return Task.FromResult(list.Where(d => d.Timestamp >= since).ToArray());
                }
            }
            return Task.FromResult(Array.Empty<RegionDelta>());
        }

        public Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot)
        {
            // For tests, simply save the provided base snapshot and clear deltas
            var regions = _worldIdToRegionIdToSnapshot.GetOrAdd(worldId, _ => new());
            regions[regionId] = baseSnapshot;
            if (_worldIdToRegionIdToDeltas.TryGetValue(worldId, out var deltas))
            {
                deltas.TryRemove(regionId, out _);
            }
            return Task.CompletedTask;
        }

        public Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta)
        {
            var regions = _worldIdToRegionIdToMapDeltas.GetOrAdd(worldId, _ => new());
            var list = regions.GetOrAdd(regionId, _ => new());
            lock (list) { list.Add(delta); }
            return Task.CompletedTask;
        }

        public Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence)
        {
            if (_worldIdToRegionIdToMapDeltas.TryGetValue(worldId, out var regions) &&
                regions.TryGetValue(regionId, out var list))
            {
                lock (list)
                {
                    return Task.FromResult(list.Where(d => d.Sequence > sinceSequence).OrderBy(d => d.Sequence).ToArray());
                }
            }
            return Task.FromResult(Array.Empty<MapDelta>());
        }

        public Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence)
        {
            if (_worldIdToRegionIdToMapDeltas.TryGetValue(worldId, out var regions) &&
                regions.TryGetValue(regionId, out var list))
            {
                lock (list) { list.RemoveAll(d => d.Sequence <= throughSequence); }
            }
            return Task.CompletedTask;
        }
    }
}


