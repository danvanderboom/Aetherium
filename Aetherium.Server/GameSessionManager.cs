using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldBuilders;

namespace Aetherium.Server
{
    /// <summary>
    /// Manages active game sessions for connected clients.
    /// Supports both single-world (legacy) and multi-world modes.
    /// </summary>
    public class GameSessionManager
    {
        private readonly ConcurrentDictionary<string, GameSession> sessions = new ConcurrentDictionary<string, GameSession>();

        /// <summary>
        /// Creates a session with a world builder (legacy single-world mode).
        /// </summary>
        public GameSession CreateSession(string connectionId, WorldBuilder worldBuilder)
        {
            var session = new GameSession(connectionId, worldBuilder);
            sessions[connectionId] = session;
            return session;
        }

        /// <summary>
        /// Creates a session with an existing world (multi-world mode).
        /// </summary>
        public GameSession CreateSession(string connectionId, string worldId, World world, WorldLocation? startLocation = null)
        {
            var session = new GameSession(connectionId, worldId, world, startLocation);
            sessions[connectionId] = session;
            return session;
        }

        public GameSession? GetSession(string connectionId)
        {
            sessions.TryGetValue(connectionId, out var session);
            return session;
        }

        public bool RemoveSession(string connectionId)
        {
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



