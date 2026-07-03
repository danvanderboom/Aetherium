using System;
using System.Collections.Generic;
using System.Text.Json;
using Orleans;
using Aetherium.Core;

namespace Aetherium.Server.Agents.Telemetry
{
    /// <summary>
    /// Stores world state and action sequences for failed agent runs to enable replay analysis.
    /// </summary>
    public sealed class ReplayData
    {
        public string ReplayId { get; set; } = Guid.NewGuid().ToString();
        public string AgentId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string BenchmarkName { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public int TotalSteps { get; set; }

        // Note: InitialWorldState is intentionally not serialized to avoid requiring Orleans serializers for World
        public World? InitialWorldState { get; set; }

        public List<ReplayStep> Steps { get; set; } = new List<ReplayStep>();

        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A single step in a replay sequence.
    /// </summary>
    public sealed class ReplayStep
    {
        public int StepNumber { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string ActionSummary { get; set; } = string.Empty;

        public Dictionary<string, object> ActionArgs { get; set; } = new Dictionary<string, object>();
        public bool Succeeded { get; set; }
        public string? PerceptionJson { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// In-memory storage for replay data. In production, this could be backed by persistent storage.
    /// </summary>
    public static class ReplayStorage
    {
        // Replays are the heaviest telemetry payload (full world state + per-step
        // perception JSON, easily MBs each) and this store is process-lifetime static,
        // so unbounded growth exhausts memory in long training runs. Oldest-first
        // eviction once the cap is hit.
        private const int MaxStoredReplays = 200;

        private static readonly Dictionary<string, ReplayData> _replays = new Dictionary<string, ReplayData>();
        private static readonly Dictionary<string, string> _replaysJson = new Dictionary<string, string>();
        private static readonly Queue<string> _replayOrder = new Queue<string>();
        private static readonly Queue<string> _replayJsonOrder = new Queue<string>();
        private static readonly object _lock = new object();

        public static string StoreReplay(ReplayData replay)
        {
            lock (_lock)
            {
                if (!_replays.ContainsKey(replay.ReplayId))
                    _replayOrder.Enqueue(replay.ReplayId);
                _replays[replay.ReplayId] = replay;
                EvictOldest(_replays, _replayOrder);
                return replay.ReplayId;
            }
        }

        public static string StoreReplayJson(string replayJson)
        {
            lock (_lock)
            {
                var id = Guid.NewGuid().ToString();
                _replaysJson[id] = replayJson;
                _replayJsonOrder.Enqueue(id);
                EvictOldest(_replaysJson, _replayJsonOrder);
                return id;
            }
        }

        private static void EvictOldest<T>(Dictionary<string, T> store, Queue<string> order)
        {
            while (store.Count > MaxStoredReplays && order.Count > 0)
            {
                var oldest = order.Dequeue();
                // Ids deleted out-of-band (DeleteReplay) may already be gone; the
                // loop keeps dequeuing until an actual eviction happens.
                store.Remove(oldest);
            }
        }

        public static ReplayData? GetReplay(string replayId)
        {
            lock (_lock)
            {
                return _replays.TryGetValue(replayId, out var replay) ? replay : null;
            }
        }

        public static string? GetReplayJson(string replayId)
        {
            lock (_lock)
            {
                return _replaysJson.TryGetValue(replayId, out var json) ? json : null;
            }
        }

        public static List<ReplayData> GetReplaysForAgent(string agentId, int? limit = null)
        {
            lock (_lock)
            {
                var results = new List<ReplayData>();
                foreach (var replay in _replays.Values)
                {
                    if (replay.AgentId == agentId)
                    {
                        results.Add(replay);
                    }
                }

                results.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

                if (limit.HasValue && results.Count > limit.Value)
                {
                    results = results.GetRange(0, limit.Value);
                }

                return results;
            }
        }

        public static List<ReplayData> GetAllReplays(int? limit = null)
        {
            lock (_lock)
            {
                var results = new List<ReplayData>(_replays.Values);
                results.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

                if (limit.HasValue && results.Count > limit.Value)
                {
                    results = results.GetRange(0, limit.Value);
                }

                return results;
            }
        }

        public static void DeleteReplay(string replayId)
        {
            lock (_lock)
            {
                _replays.Remove(replayId);
            }
        }

        public static int GetReplayCount()
        {
            lock (_lock)
            {
                return _replays.Count;
            }
        }
    }
}

