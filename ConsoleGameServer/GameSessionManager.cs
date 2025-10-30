using System;
using System.Collections.Concurrent;
using ConsoleGame.WorldBuilders;

namespace ConsoleGameServer
{
    public class GameSessionManager
    {
        private readonly ConcurrentDictionary<string, GameSession> sessions = new ConcurrentDictionary<string, GameSession>();

        public GameSession CreateSession(string connectionId, WorldBuilder worldBuilder)
        {
            var session = new GameSession(connectionId, worldBuilder);
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

        public int ActiveSessionCount => sessions.Count;
    }
}

