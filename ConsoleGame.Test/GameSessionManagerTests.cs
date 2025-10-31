using System;
using System.Linq;
using NUnit.Framework;
using ConsoleGameServer;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.WorldBuilders;

namespace ConsoleGame.Test
{
    [TestFixture]
    public class GameSessionManagerTests
    {
        private GameSessionManager _manager = null!;
        private TestMazeWorldBuilder _worldBuilder = null!;

        [SetUp]
        public void SetUp()
        {
            _manager = new GameSessionManager();
            _worldBuilder = new TestMazeWorldBuilder();
        }

        [Test]
        public void GameSessionManager_ShouldCreate_LegacySession()
        {
            // Arrange
            var connectionId = "connection-1";

            // Act
            var session = _manager.CreateSession(connectionId, _worldBuilder);

            // Assert
            Assert.That(session, Is.Not.Null);
            Assert.That(session.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(session.World, Is.Not.Null);
        }

        [Test]
        public void GameSessionManager_ShouldCreate_MultiWorldSession()
        {
            // Arrange
            var connectionId = "connection-1";
            var worldId = "world-1";
            var world = _worldBuilder.Build();
            var startLocation = new WorldLocation(5, 5, 0);

            // Act
            var session = _manager.CreateSession(connectionId, worldId, world, startLocation);

            // Assert
            Assert.That(session, Is.Not.Null);
            Assert.That(session.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(session.WorldId, Is.EqualTo(worldId));
            Assert.That(session.World, Is.SameAs(world));
        }

        [Test]
        public void GameSessionManager_ShouldGet_ExistingSession()
        {
            // Arrange
            var connectionId = "connection-1";
            var session = _manager.CreateSession(connectionId, _worldBuilder);

            // Act
            var retrieved = _manager.GetSession(connectionId);

            // Assert
            Assert.That(retrieved, Is.SameAs(session));
        }

        [Test]
        public void GameSessionManager_ShouldReturn_NullForInvalidConnection()
        {
            // Arrange & Act
            var retrieved = _manager.GetSession("nonexistent-connection");

            // Assert
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void GameSessionManager_ShouldRemove_Session()
        {
            // Arrange
            var connectionId = "connection-1";
            _manager.CreateSession(connectionId, _worldBuilder);

            // Act
            var removed = _manager.RemoveSession(connectionId);
            var retrieved = _manager.GetSession(connectionId);

            // Assert
            Assert.That(removed, Is.True);
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void GameSessionManager_ShouldGet_SessionsInWorld()
        {
            // Arrange
            var worldId1 = "world-1";
            var worldId2 = "world-2";
            var world1 = _worldBuilder.Build();
            var world2 = _worldBuilder.Build();
            
            _manager.CreateSession("conn-1", worldId1, world1);
            _manager.CreateSession("conn-2", worldId1, world1);
            _manager.CreateSession("conn-3", worldId2, world2);

            // Act
            var world1Sessions = _manager.GetSessionsInWorld(worldId1);
            var world2Sessions = _manager.GetSessionsInWorld(worldId2);

            // Assert
            Assert.That(world1Sessions.Count, Is.EqualTo(2));
            Assert.That(world2Sessions.Count, Is.EqualTo(1));
        }

        [Test]
        public void GameSessionManager_ShouldCount_PlayersInWorld()
        {
            // Arrange
            var worldId1 = "world-1";
            var worldId2 = "world-2";
            var world1 = _worldBuilder.Build();
            var world2 = _worldBuilder.Build();
            
            _manager.CreateSession("conn-1", worldId1, world1);
            _manager.CreateSession("conn-2", worldId1, world1);
            _manager.CreateSession("conn-3", worldId2, world2);

            // Act
            var count1 = _manager.GetWorldPlayerCount(worldId1);
            var count2 = _manager.GetWorldPlayerCount(worldId2);

            // Assert
            Assert.That(count1, Is.EqualTo(2));
            Assert.That(count2, Is.EqualTo(1));
        }

        [Test]
        public void GameSessionManager_ShouldReturn_ZeroForEmptyWorld()
        {
            // Arrange
            var worldId = "empty-world";

            // Act
            var count = _manager.GetWorldPlayerCount(worldId);

            // Assert
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void GameSessionManager_ShouldHandle_SessionReplacement()
        {
            // Arrange
            var connectionId = "connection-1";
            var world1 = _worldBuilder.Build();
            var world2 = _worldBuilder.Build();
            var session1 = _manager.CreateSession(connectionId, "world-1", world1);

            // Act - Create another session with same connection ID
            var session2 = _manager.CreateSession(connectionId, "world-2", world2);
            var retrieved = _manager.GetSession(connectionId);

            // Assert - Should have the newer session
            Assert.That(retrieved, Is.SameAs(session2));
            Assert.That(retrieved, Is.Not.SameAs(session1));
        }

        [Test]
        public void GameSessionManager_ShouldUpdate_WorldPlayerCount_OnRemove()
        {
            // Arrange
            var worldId = "world-1";
            var world = _worldBuilder.Build();
            _manager.CreateSession("conn-1", worldId, world);
            _manager.CreateSession("conn-2", worldId, world);

            // Act
            _manager.RemoveSession("conn-1");
            var count = _manager.GetWorldPlayerCount(worldId);

            // Assert
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void GameSessionManager_ShouldTrack_MultipleConcurrentWorlds()
        {
            // Arrange
            var world1 = _worldBuilder.Build();
            var world2 = _worldBuilder.Build();
            var world3 = _worldBuilder.Build();
            
            _manager.CreateSession("conn-1", "world-1", world1);
            _manager.CreateSession("conn-2", "world-1", world1);
            _manager.CreateSession("conn-3", "world-2", world2);
            _manager.CreateSession("conn-4", "world-2", world2);
            _manager.CreateSession("conn-5", "world-2", world2);
            _manager.CreateSession("conn-6", "world-3", world3);

            // Act
            var count1 = _manager.GetWorldPlayerCount("world-1");
            var count2 = _manager.GetWorldPlayerCount("world-2");
            var count3 = _manager.GetWorldPlayerCount("world-3");

            // Assert
            Assert.That(count1, Is.EqualTo(2));
            Assert.That(count2, Is.EqualTo(3));
            Assert.That(count3, Is.EqualTo(1));
        }

        [Test]
        public void GameSessionManager_ShouldHandle_SessionWithoutWorldId()
        {
            // Arrange & Act
            var session = _manager.CreateSession("conn-1", _worldBuilder);

            // Assert - WorldId should be null for legacy sessions
            Assert.That(session.WorldId, Is.Null);
        }

        [Test]
        public void GameSessionManager_ShouldNotCount_SessionsWithoutWorldId()
        {
            // Arrange
            var worldId = "world-1";
            var world = _worldBuilder.Build();
            _manager.CreateSession("conn-1", _worldBuilder); // No world ID
            _manager.CreateSession("conn-2", worldId, world); // With world ID

            // Act
            var count = _manager.GetWorldPlayerCount(worldId);

            // Assert
            Assert.That(count, Is.EqualTo(1), "Should only count sessions with world ID");
        }

        [Test]
        public void GameSessionManager_ShouldAllow_MultipleSessionsInSameWorld()
        {
            // Arrange
            var worldId = "world-1";
            var world = _worldBuilder.Build();

            // Act
            var session1 = _manager.CreateSession("conn-1", worldId, world);
            var session2 = _manager.CreateSession("conn-2", worldId, world);
            var session3 = _manager.CreateSession("conn-3", worldId, world);

            // Assert
            Assert.That(session1.WorldId, Is.EqualTo(worldId));
            Assert.That(session2.WorldId, Is.EqualTo(worldId));
            Assert.That(session3.WorldId, Is.EqualTo(worldId));
            
            var worldSessions = _manager.GetSessionsInWorld(worldId);
            Assert.That(worldSessions.Count, Is.EqualTo(3));
        }

        [Test]
        public void GameSessionManager_ShouldRemove_SpecificSessionFromWorld()
        {
            // Arrange
            var worldId = "world-1";
            var world = _worldBuilder.Build();
            _manager.CreateSession("conn-1", worldId, world);
            _manager.CreateSession("conn-2", worldId, world);
            _manager.CreateSession("conn-3", worldId, world);

            // Act
            _manager.RemoveSession("conn-2");
            var worldSessions = _manager.GetSessionsInWorld(worldId);

            // Assert
            Assert.That(worldSessions.Count, Is.EqualTo(2));
            Assert.That(worldSessions.Any(s => s.ConnectionId == "conn-1"), Is.True);
            Assert.That(worldSessions.Any(s => s.ConnectionId == "conn-2"), Is.False);
            Assert.That(worldSessions.Any(s => s.ConnectionId == "conn-3"), Is.True);
        }

        [Test]
        public void GameSessionManager_ShouldHandle_EmptySessionList()
        {
            // Arrange & Act
            var worldSessions = _manager.GetSessionsInWorld("world-1");
            var playerCount = _manager.GetWorldPlayerCount("world-1");

            // Assert
            Assert.That(worldSessions, Is.Empty);
            Assert.That(playerCount, Is.EqualTo(0));
        }

        [Test]
        public void GameSessionManager_ShouldNotThrow_OnRemovingNonexistentSession()
        {
            // Arrange & Act & Assert
            Assert.DoesNotThrow(() => {
                var result = _manager.RemoveSession("nonexistent-connection");
                Assert.That(result, Is.False);
            });
        }

        [Test]
        public void GameSessionManager_ShouldPreserve_PlayerEntity()
        {
            // Arrange
            var connectionId = "connection-1";
            var session = _manager.CreateSession(connectionId, _worldBuilder);

            // Act
            var retrieved = _manager.GetSession(connectionId);

            // Assert - Player entity should be preserved
            Assert.That(retrieved?.Player, Is.Not.Null);
            Assert.That(retrieved?.Player, Is.SameAs(session.Player));
        }

        [Test]
        public void GameSessionManager_ShouldSet_CustomStartLocation()
        {
            // Arrange
            var connectionId = "connection-1";
            var worldId = "world-1";
            var world = _worldBuilder.Build();
            var customStart = new WorldLocation(10, 15, 0);

            // Act
            var session = _manager.CreateSession(connectionId, worldId, world, customStart);

            // Assert
            var playerLoc = session.Player.Get<ConsoleGame.Components.WorldLocation>();
            Assert.That(playerLoc.X, Is.EqualTo(10));
            Assert.That(playerLoc.Y, Is.EqualTo(15));
            Assert.That(playerLoc.Z, Is.EqualTo(0));
        }

        [Test]
        public void GameSessionManager_ShouldTrack_ActiveSessionCount()
        {
            // Arrange
            var world = _worldBuilder.Build();
            _manager.CreateSession("conn-1", "world-1", world);
            _manager.CreateSession("conn-2", "world-1", world);
            _manager.CreateSession("conn-3", "world-2", world);

            // Act
            var count = _manager.ActiveSessionCount;

            // Assert
            Assert.That(count, Is.EqualTo(3));
        }
    }
}
