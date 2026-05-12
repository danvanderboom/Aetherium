using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// In-memory implementation of IWorldSnapshotStore (for development and tests).
    /// </summary>
    public class MemoryWorldSnapshotStore : IWorldSnapshotStore
    {
        private readonly ConcurrentDictionary<string, RegionStateSnapshot> _snapshots = new();
        private readonly ConcurrentDictionary<string, List<RegionDelta>> _deltaLogs = new();
        private readonly ConcurrentDictionary<string, List<MapDelta>> _mapDeltaLogs = new();

        public Task SaveSnapshotAsync(string worldId, RegionStateSnapshot snapshot)
        {
            var key = GetSnapshotKey(worldId, snapshot.RegionId);
            _snapshots[key] = snapshot;
            return Task.CompletedTask;
        }

        public Task<RegionStateSnapshot?> LoadSnapshotAsync(string worldId, string regionId)
        {
            var key = GetSnapshotKey(worldId, regionId);
            _snapshots.TryGetValue(key, out var snapshot);
            return Task.FromResult<RegionStateSnapshot?>(snapshot);
        }

        public Task DeleteSnapshotAsync(string worldId, string regionId)
        {
            var key = GetSnapshotKey(worldId, regionId);
            _snapshots.TryRemove(key, out _);
            
            var deltaKey = GetDeltaLogKey(worldId, regionId);
            _deltaLogs.TryRemove(deltaKey, out _);
            
            return Task.CompletedTask;
        }

        public Task<string[]> ListRegionIdsAsync(string worldId)
        {
            var prefix = $"{worldId}:";
            var regionIds = _snapshots.Keys
                .Where(k => k.StartsWith(prefix))
                .Select(k => k.Substring(prefix.Length))
                .ToArray();
            
            return Task.FromResult(regionIds);
        }

        public Task AppendDeltaAsync(string worldId, string regionId, RegionDelta delta)
        {
            var key = GetDeltaLogKey(worldId, regionId);
            var log = _deltaLogs.GetOrAdd(key, _ => new List<RegionDelta>());
            lock (log)
            {
                log.Add(delta);
            }
            return Task.CompletedTask;
        }

        public Task<RegionDelta[]> GetDeltasSinceAsync(string worldId, string regionId, DateTime since)
        {
            var key = GetDeltaLogKey(worldId, regionId);
            if (!_deltaLogs.TryGetValue(key, out var log))
                return Task.FromResult(Array.Empty<RegionDelta>());
            
            RegionDelta[] deltas;
            lock (log)
            {
                deltas = log.Where(d => d.Timestamp >= since).ToArray();
            }
            
            return Task.FromResult(deltas);
        }

        public Task CompactDeltasAsync(string worldId, string regionId, RegionStateSnapshot baseSnapshot)
        {
            // Apply all deltas to base snapshot
            var key = GetDeltaLogKey(worldId, regionId);
            if (!_deltaLogs.TryGetValue(key, out var log))
                return Task.CompletedTask;
            
            var snapshot = baseSnapshot;
            lock (log)
            {
                foreach (var delta in log)
                {
                    snapshot = ApplyDelta(snapshot, delta);
                }
                log.Clear();
            }
            
            // Save compacted snapshot
            return SaveSnapshotAsync(worldId, snapshot);
        }

        private RegionStateSnapshot ApplyDelta(RegionStateSnapshot snapshot, RegionDelta delta)
        {
            // Apply delta modifications to snapshot
            // This is a simplified implementation; full version would handle all delta types
            switch (delta.Type)
            {
                case DeltaType.TerrainModified:
                    if (delta.Data.TryGetValue("location", out var loc) && 
                        delta.Data.TryGetValue("terrainType", out var terrainType))
                    {
                        snapshot.TerrainModifications[loc.ToString()!] = terrainType.ToString()!;
                    }
                    break;
                case DeltaType.TraversalRecorded:
                    if (delta.Data.TryGetValue("location", out var travLoc) &&
                        delta.Data.TryGetValue("count", out var count))
                    {
                        snapshot.TraversalHeatmap[travLoc.ToString()!] = Convert.ToInt32(count);
                    }
                    break;
                // Add other delta types as needed
            }
            
            return snapshot;
        }

        public Task AppendMapDeltaAsync(string worldId, string regionId, MapDelta delta)
        {
            var key = GetDeltaLogKey(worldId, regionId);
            var log = _mapDeltaLogs.GetOrAdd(key, _ => new List<MapDelta>());
            lock (log)
            {
                log.Add(delta);
            }
            return Task.CompletedTask;
        }

        public Task<MapDelta[]> GetMapDeltasSinceSequenceAsync(string worldId, string regionId, long sinceSequence)
        {
            var key = GetDeltaLogKey(worldId, regionId);
            if (!_mapDeltaLogs.TryGetValue(key, out var log))
                return Task.FromResult(Array.Empty<MapDelta>());

            MapDelta[] deltas;
            lock (log)
            {
                deltas = log.Where(d => d.Sequence > sinceSequence).OrderBy(d => d.Sequence).ToArray();
            }
            return Task.FromResult(deltas);
        }

        public Task CompactMapDeltasAsync(string worldId, string regionId, long throughSequence)
        {
            var key = GetDeltaLogKey(worldId, regionId);
            if (!_mapDeltaLogs.TryGetValue(key, out var log))
                return Task.CompletedTask;
            lock (log)
            {
                log.RemoveAll(d => d.Sequence <= throughSequence);
            }
            return Task.CompletedTask;
        }

        public Task DeleteWorldAsync(string worldId)
        {
            var snapshotPrefix = $"{worldId}:snapshot:";
            var deltaPrefix = $"{worldId}:deltas:";
            foreach (var key in _snapshots.Keys.Where(k => k.StartsWith(snapshotPrefix)).ToList())
                _snapshots.TryRemove(key, out _);
            foreach (var key in _deltaLogs.Keys.Where(k => k.StartsWith(deltaPrefix)).ToList())
                _deltaLogs.TryRemove(key, out _);
            foreach (var key in _mapDeltaLogs.Keys.Where(k => k.StartsWith(deltaPrefix)).ToList())
                _mapDeltaLogs.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        private static string GetSnapshotKey(string worldId, string regionId) => $"{worldId}:snapshot:{regionId}";
        private static string GetDeltaLogKey(string worldId, string regionId) => $"{worldId}:deltas:{regionId}";
    }
}

