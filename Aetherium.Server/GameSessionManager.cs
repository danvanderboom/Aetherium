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

        // Sessions whose connection dropped but whose state is being kept alive for the
        // resume grace window, keyed by SessionId. A reconnecting client presenting the
        // matching resume token rebinds to the entry; entries older than
        // ResumeGraceWindow are cleaned up (SessionExpired) and become unresumable.
        private readonly ConcurrentDictionary<string, DetachedSession> detached = new ConcurrentDictionary<string, DetachedSession>();

        private sealed class DetachedSession
        {
            public DetachedSession(GameSession session, DateTime detachedAtUtc)
            {
                Session = session;
                DetachedAtUtc = detachedAtUtc;
            }
            public GameSession Session { get; }
            public DateTime DetachedAtUtc { get; }
        }

        /// <summary>
        /// How long a disconnected session stays resumable. Mutable so tests can shrink
        /// it; ops can override the default via AETHERIUM_RESUME_GRACE_SECONDS.
        /// </summary>
        public TimeSpan ResumeGraceWindow { get; set; } = ResolveDefaultGraceWindow();

        private static TimeSpan ResolveDefaultGraceWindow()
        {
            var raw = Environment.GetEnvironmentVariable("AETHERIUM_RESUME_GRACE_SECONDS");
            if (int.TryParse(raw, out var seconds) && seconds >= 0)
                return TimeSpan.FromSeconds(seconds);
            return TimeSpan.FromSeconds(90);
        }

        /// <summary>
        /// Deferred disconnect cleanup, run once per session when its grace window lapses
        /// without a resume (or when a resume arrives too late). Wired in Program.cs to
        /// the grain-side teardown (management-grain unregister + map-grain LeavePlayer)
        /// that used to run inline in GameHub.OnDisconnectedAsync.
        /// </summary>
        public Func<GameSession, Task>? SessionExpired { get; set; }

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
        /// Batch variant of <see cref="NotifyMapMutationAsync"/>: applies a whole tick's worth of
        /// deltas (e.g. every roaming monster's move) to each affected session under a single lock
        /// acquisition, then schedules ONE perception flush. Applying deltas one-at-a-time made the
        /// NPC tick acquire the session lock once per monster — each contending with the perception
        /// flush that holds the same lock — which is what pinned the map grain and starved player
        /// input. The debounced flush already coalesces the frame, so a batch still yields at most
        /// one pushed frame per window.
        /// </summary>
        public Task NotifyMapMutationsAsync(string mapId, System.Collections.Generic.IReadOnlyList<MapDelta> deltas)
        {
            if (string.IsNullOrEmpty(mapId) || deltas is null || deltas.Count == 0) return Task.CompletedTask;

            var affected = sessions.Values
                .Where(s => s.MapId == mapId)
                .ToList();

            foreach (var session in affected)
            {
                try
                {
                    session.ApplyDeltas(deltas);
                    if (!string.IsNullOrEmpty(session.ConnectionId))
                        dirtyPerception[session.ConnectionId] = 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameSessionManager] Batch delta application failed for session {session.SessionId}: {ex.Message}");
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
        /// Completes once the debounced perception fan-out has fully drained: no session is
        /// still marked dirty and no flush task is pending or mid-flight. Because
        /// <see cref="SchedulePerceptionFlush"/> dispatches ~<see cref="PerceptionFlushWindowMs"/>ms
        /// after a mutation, a mutation's <c>ReceivePerceptionUpdate</c> can land well after the
        /// grain call that produced it has returned. A caller that needs a clean observation
        /// boundary — e.g. a test about to reset its dispatch capture and assert which
        /// connections receive the NEXT mutation — must await this first so an earlier
        /// mutation's in-flight flush doesn't bleed across the boundary. Polls until quiescent
        /// or <paramref name="timeout"/> elapses (returns either way; it's a settle, not a gate).
        /// </summary>
        public async Task WaitForPerceptionQuiescenceAsync(TimeSpan timeout)
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < timeout)
            {
                if (dirtyPerception.IsEmpty && System.Threading.Volatile.Read(ref perceptionFlushScheduled) == 0)
                    return;
                await Task.Delay(10);
            }
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

        /// <summary>
        /// Creates a headless session (no interactive client) bound to an existing world.
        /// Places a player character exactly as a normal join would, and tags the session as headless.
        /// </summary>
        public GameSession CreateHeadlessSession(string connectionId, string worldId, World world, WorldLocation? startLocation = null)
        {
            var session = new GameSession(connectionId, worldId, world, startLocation) { IsHeadless = true };
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

        /// <summary>
        /// Re-points a live session onto a different world/map and pushes a fresh perception frame —
        /// the reusable session half of the world/map perception re-point (add-boardable-vehicles
        /// Phase 0). Swaps the session's <see cref="GameSession.World"/> to one hydrated from
        /// <paramref name="builder"/> (a target-map snapshot), rebinds
        /// <see cref="GameSession.WorldId"/>/<see cref="GameSession.MapId"/> and re-anchors the view at
        /// <paramref name="spawn"/>, optionally swaps the mutation gateway to the target map, runs
        /// <paramref name="configure"/> (e.g. to apply per-game player vision) before the push, then
        /// sends <c>ReceivePerceptionUpdate</c> to the session's connection.
        ///
        /// <para>
        /// This runs silo-side, so it is callable both from <c>GameHub</c> (JoinWorld / UsePortal) and
        /// directly from grains that move a player between maps (e.g. a vehicle boarding a party into
        /// its interior). Returns false when no live session matches <paramref name="sessionId"/> — a
        /// detached/headless session is simply not re-pointed.
        /// </para>
        /// </summary>
        public async Task<bool> RepointSessionAsync(
            string sessionId,
            Aetherium.WorldBuilders.WorldBuilder builder,
            string worldId,
            string mapId,
            Aetherium.Components.WorldLocation spawn,
            Aetherium.Server.MultiWorld.IMapMutationGateway? gateway = null,
            Action<GameSession>? configure = null)
        {
            if (string.IsNullOrEmpty(sessionId) || builder is null)
                return false;

            var session = sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
            if (session is null)
                return false;

            session.ReplaceWorld(builder, worldId, mapId, spawn);
            if (gateway is not null)
                session.Gateway = gateway;
            configure?.Invoke(session);

            // The mirror moved to a new map, so any pending coalesced flush for the old map is stale.
            dirtyPerception.TryRemove(session.ConnectionId, out _);

            if (hubContext is not null && !string.IsNullOrEmpty(session.ConnectionId))
            {
                var perception = session.GetPerception();
                await hubContext.Clients.Client(session.ConnectionId)
                    .SendAsync("ReceivePerceptionUpdate", perception);
            }

            return true;
        }

        public bool RemoveSession(string connectionId)
        {
            dirtyPerception.TryRemove(connectionId, out _);
            return sessions.TryRemove(connectionId, out _);
        }

        /// <summary>
        /// Moves the session bound to <paramref name="connectionId"/> into the detached
        /// (resumable) set instead of destroying it, and schedules its expiry. Returns the
        /// detached session, or null when the connection had no session — e.g. it was
        /// already rebound to a newer connection by <see cref="TryResumeSession"/>.
        /// </summary>
        public GameSession? DetachSession(string connectionId)
        {
            dirtyPerception.TryRemove(connectionId, out _);
            if (!sessions.TryRemove(connectionId, out var session))
                return null;

            var entry = new DetachedSession(session, DateTime.UtcNow);
            detached[session.SessionId] = entry;
            ScheduleExpiry(session.SessionId, entry, ResumeGraceWindow);
            return session;
        }

        /// <summary>
        /// Rebinds the session identified by <paramref name="sessionId"/> to
        /// <paramref name="newConnectionId"/> when the caller presents the matching
        /// <paramref name="resumeToken"/> and the grace window hasn't lapsed. Handles both
        /// the normal case (session detached by a prior disconnect) and the race where the
        /// old connection hasn't been torn down yet (session still active) — the later
        /// OnDisconnectedAsync for the old connection then finds nothing to detach.
        /// Returns null on unknown session, bad token, or expiry (expiry cleanup runs).
        /// </summary>
        public GameSession? TryResumeSession(string sessionId, string resumeToken, string newConnectionId)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(resumeToken) || string.IsNullOrEmpty(newConnectionId))
                return null;

            if (detached.TryGetValue(sessionId, out var entry))
            {
                if (!TokenMatches(entry.Session, resumeToken))
                    return null; // leave the entry for its rightful owner

                if (DateTime.UtcNow - entry.DetachedAtUtc > ResumeGraceWindow)
                {
                    _ = ExpireNow(sessionId);
                    return null;
                }

                if (!detached.TryRemove(sessionId, out entry))
                    return null; // lost a race with expiry or another resume

                return Rebind(entry.Session, newConnectionId);
            }

            // Still-active session (server never saw the old connection drop): steal it.
            var active = sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
            if (active is null || !TokenMatches(active, resumeToken))
                return null;
            if (active.ConnectionId == newConnectionId)
                return active; // idempotent re-resume on the same connection

            sessions.TryRemove(active.ConnectionId, out _);
            dirtyPerception.TryRemove(active.ConnectionId, out _);
            return Rebind(active, newConnectionId);
        }

        private static bool TokenMatches(GameSession session, string resumeToken)
            => string.Equals(session.ResumeToken, resumeToken, StringComparison.Ordinal);

        private GameSession Rebind(GameSession session, string newConnectionId)
        {
            session.ConnectionId = newConnectionId;
            sessions[newConnectionId] = session;
            return session;
        }

        private void ScheduleExpiry(string sessionId, DetachedSession entry, TimeSpan delay)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay);
                    // Only expire the same detach epoch: if the session was resumed and
                    // detached again since, the current entry differs and its own timer
                    // is running.
                    if (detached.TryGetValue(sessionId, out var current) && ReferenceEquals(current, entry))
                        await ExpireNow(sessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameSessionManager] Expiry sweep for {sessionId} failed: {ex.Message}");
                }
            });
        }

        private async Task ExpireNow(string sessionId)
        {
            if (!detached.TryRemove(sessionId, out var entry))
                return;

            Console.WriteLine($"[GameSessionManager] Session {sessionId} grace window lapsed; running deferred cleanup");
            var cleanup = SessionExpired;
            if (cleanup is null)
                return;
            try
            {
                await cleanup(entry.Session);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSessionManager] Expiry cleanup for {sessionId} failed: {ex.Message}");
            }
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



