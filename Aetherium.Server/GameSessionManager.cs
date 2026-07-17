using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;
using Microsoft.AspNetCore.SignalR;

namespace Aetherium.Server
{
    /// <summary>
    /// Manages active game sessions for connected clients.
    /// Supports both single-world (legacy) and multi-world modes.
    /// </summary>
    public class GameSessionManager
    {
        private readonly ConcurrentDictionary<string, GameSession> sessions = new ConcurrentDictionary<string, GameSession>();
        private readonly PerceptionService? perceptionService;
        private readonly IHubContext<GameHub>? hubContext;

        // Perception-push debounce (see NotifyMapMutationAsync): connectionIds whose mirror
        // changed since their last push, and a 0/1 flag for whether a flush is scheduled.
        // Deltas are applied immediately; frames are coalesced to at most one per session per
        // window, so a map full of wandering NPCs can't turn every tick into hundreds of
        // raycast perception computes inside the grain's message loop.
        private readonly ConcurrentDictionary<string, byte> dirtyPerception = new ConcurrentDictionary<string, byte>();
        private int perceptionFlushScheduled;
        private const int PerceptionFlushWindowMs = 100;

        public GameSessionManager(PerceptionService? perceptionService = null, IHubContext<GameHub>? hubContext = null)
        {
            this.perceptionService = perceptionService;
            this.hubContext = hubContext;
        }

        /// <summary>
        /// Applies a grain-emitted <see cref="MapDelta"/> to every session bound to
        /// <paramref name="mapId"/>, then pushes a fresh perception update to each
        /// affected client over SignalR.
        ///
        /// <para>
        /// Phase 2c: this is the host-side bridge between the grain's authoritative
        /// state and the per-session mirrors. Critically, deltas are applied
        /// server-side and clients only see the resulting <c>ReceivePerceptionUpdate</c>
        /// — never the raw deltas — which preserves the engine's perception-pure
        /// invariant (clients never learn about cells outside their FOV).
        /// </para>
        /// </summary>
        /// <summary>
        /// Applies a MapDelta to a single session by id (used for actor-only deltas
        /// such as <see cref="EntityHeadingChangedDelta"/>). The session reconciles
        /// its mirror; the manager then pushes a fresh perception update over SignalR.
        /// </summary>
        public async Task NotifySessionMutationAsync(string sessionId, MapDelta delta)
        {
            if (string.IsNullOrEmpty(sessionId) || delta is null) return;

            // sessions is keyed by connectionId, so find by SessionId.
            var session = sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
            if (session is null) return;

            try
            {
                session.ApplyDelta(delta);
                if (hubContext is not null && !string.IsNullOrEmpty(session.ConnectionId))
                {
                    var perception = session.GetPerception();
                    await hubContext.Clients.Client(session.ConnectionId)
                        .SendAsync("ReceivePerceptionUpdate", perception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSessionManager] Targeted delta failed for {sessionId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a named, player-scoped event directly to one session's client — not a
        /// <see cref="MapDelta"/>, so it does not touch the session's world mirror or trigger a
        /// perception push. Used for player-lifecycle signals (engine gap-analysis §4.11, Phase 2 —
        /// see wire-death-respawn-live) such as <c>ReceiveDowned</c>/<c>ReceiveRespawn</c>/
        /// <c>ReceiveDied</c>, which describe what's happening to the player rather than a change to
        /// world state.
        /// </summary>
        public async Task NotifyPlayerEventAsync(string sessionId, string methodName, object payload)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(methodName)) return;

            var session = sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
            if (session is null || hubContext is null || string.IsNullOrEmpty(session.ConnectionId)) return;

            try
            {
                await hubContext.Clients.Client(session.ConnectionId).SendAsync(methodName, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSessionManager] Player event '{methodName}' failed for {sessionId}: {ex.Message}");
            }
        }

        public Task NotifyMapMutationAsync(string mapId, MapDelta delta)
        {
            if (string.IsNullOrEmpty(mapId) || delta is null) return Task.CompletedTask;

            // Snapshot the affected sessions first so we don't hold the dictionary
            // enumerator while doing I/O.
            var affected = sessions.Values
                .Where(s => s.MapId == mapId)
                .ToList();

            foreach (var session in affected)
            {
                try
                {
                    // Apply immediately — the mirror must never lag the authoritative
                    // stream — but DON'T compute/push perception per delta. A tick's worth
                    // of NPC movement is dozens-to-hundreds of deltas; computing a full
                    // raycast perception frame for each one (as this method originally did)
                    // multiplied tick cost by the delta count, snowballed the grain's
                    // message loop into multi-second stalls, and firehosed clients with
                    // hundreds of frames per second until the connection starved and died.
                    session.ApplyDelta(delta);

                    if (!string.IsNullOrEmpty(session.ConnectionId))
                        dirtyPerception[session.ConnectionId] = 1;
                }
                catch (Exception ex)
                {
                    // One session's reconciliation failure must not stop the others.
                    Console.WriteLine($"[GameSessionManager] Delta application failed for session {session.SessionId}: {ex.Message}");
                }
            }

            SchedulePerceptionFlush();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Debounced perception fan-out: at most one frame per dirty session per
        /// <see cref="PerceptionFlushWindowMs"/> window, coalescing however many deltas
        /// landed in between. Each flush computes perception fresh (FOV filtering,
        /// lighting, vision-mode application — the same code path as a player-initiated
        /// update), so the client always receives current state; the client's
        /// MoveSequence staleness handling covers any interleaving with tool-response
        /// pushes. Reschedules itself if new deltas arrive while a flush is running.
        /// </summary>
        private void SchedulePerceptionFlush()
        {
            if (hubContext is null || dirtyPerception.IsEmpty)
                return;
            if (System.Threading.Interlocked.Exchange(ref perceptionFlushScheduled, 1) == 1)
                return; // a flush is already pending; it will pick these sessions up

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(PerceptionFlushWindowMs);

                    foreach (var connectionId in dirtyPerception.Keys.ToList())
                    {
                        if (!dirtyPerception.TryRemove(connectionId, out _))
                            continue;
                        if (!sessions.TryGetValue(connectionId, out var session))
                            continue;

                        try
                        {
                            var perception = session.GetPerception();
                            await hubContext.Clients.Client(connectionId)
                                .SendAsync("ReceivePerceptionUpdate", perception);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GameSessionManager] Perception flush failed for session {session.SessionId}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref perceptionFlushScheduled, 0);
                    // Deltas that arrived after the key snapshot above stay dirty — kick
                    // another window so they aren't stranded until the next mutation.
                    SchedulePerceptionFlush();
                }
            });
        }

        /// <summary>
        /// Creates a session with a world builder (legacy single-world mode).
        /// </summary>
        public GameSession CreateSession(string connectionId, WorldBuilder worldBuilder)
        {
            var session = new GameSession(connectionId, worldBuilder, perceptionService);
            sessions[connectionId] = session;
            return session;
        }

        /// <summary>
        /// Creates a session with an existing world (multi-world mode).
        /// </summary>
        public GameSession CreateSession(string connectionId, string worldId, World world, WorldLocation? startLocation = null)
        {
            var session = new GameSession(connectionId, worldId, world, startLocation, perceptionService);
            sessions[connectionId] = session;
            return session;
        }

        public GameSession? GetSession(string connectionId)
        {
            sessions.TryGetValue(connectionId, out var session);
            return session;
        }

        /// <summary>
        /// Replaces an existing session's <see cref="GameSession.World"/> with one built
        /// from <paramref name="builder"/>. Used by <c>GameHub.JoinWorld</c> to swap
        /// from the legacy private world to a grain-snapshot-hydrated world.
        ///
        /// <para>
        /// PHASE 1: the new world is independent per-session. See
        /// <see cref="GameSession.ReplaceWorld"/> for semantics.
        /// </para>
        /// </summary>
        public void ReplaceSessionWorld(
            GameSession session,
            Aetherium.WorldBuilders.WorldBuilder builder,
            string worldId,
            string mapId,
            Aetherium.Components.WorldLocation spawnLocation)
        {
            session.ReplaceWorld(builder, worldId, mapId, spawnLocation);
        }

        public bool RemoveSession(string connectionId)
        {
            dirtyPerception.TryRemove(connectionId, out _);
            return sessions.TryRemove(connectionId, out _);
        }

        /// <summary>
        /// Gets all sessions currently in a specific world.
        /// </summary>
        public List<GameSession> GetSessionsInWorld(string worldId)
        {
            return sessions.Values
                .Where(s => s.WorldId == worldId)
                .ToList();
        }

        /// <summary>
        /// Gets count of active sessions in a specific world.
        /// </summary>
        public int GetWorldPlayerCount(string worldId)
        {
            return sessions.Values.Count(s => s.WorldId == worldId);
        }

        public int ActiveSessionCount => sessions.Count;
    }
}



