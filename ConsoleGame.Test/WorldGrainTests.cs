using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using ConsoleGameServer.MultiWorld;
using ConsoleGame.Components;

namespace ConsoleGame.Test
{
    [TestFixture]
    public class WorldGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Orleans registers IGrainFactory; no manual registration needed in v9

                // Orleans registers IGrainFactory; no manual registration needed in v9
                // Add in-memory grain storage for testing (names must match grain attributes)
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");

                // Register map generator registry with discovered generators/features
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<ConsoleGame.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new ConsoleGame.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(ConsoleGame.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            SiloConfigurator.SetCluster(_cluster);
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task WorldGrain_ShouldInitialize_WithDefaultState()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);

            // Act
            var state = await grain.GetStateAsync();

            // Assert - New grain should be in Creating state
            Assert.That(state, Is.EqualTo(WorldState.Creating));
        }

        [Test]
        public async Task WorldGrain_ShouldInitialize_WithValidConfig()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Test World",
                Description = "A test world",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 },
                MaxPlayers = 10
            };

            // Act
            await grain.InitializeAsync(config);
            var info = await grain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.Name, Is.EqualTo("Test World"));
            Assert.That(info.Description, Is.EqualTo("A test world"));
            Assert.That(info.MaxPlayers, Is.EqualTo(10));
        }

        [Test]
        public async Task WorldGrain_ShouldTransition_BetweenStates()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "State Test World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            // Act & Assert - Initialize (should go to Active after init)
            await grain.InitializeAsync(config);
            var state = await grain.GetStateAsync();
            Assert.That(state, Is.EqualTo(WorldState.Active).Or.EqualTo(WorldState.Creating));

            // Act & Assert - Pause
            await grain.PauseAsync();
            state = await grain.GetStateAsync();
            Assert.That(state, Is.EqualTo(WorldState.Paused));

            // Act & Assert - Resume
            await grain.ResumeAsync();
            state = await grain.GetStateAsync();
            Assert.That(state, Is.EqualTo(WorldState.Active));

            // Act & Assert - Shutdown
            await grain.ShutdownAsync();
            state = await grain.GetStateAsync();
            Assert.That(state, Is.EqualTo(WorldState.ShuttingDown).Or.EqualTo(WorldState.Stopped));
        }

        [Test]
        public async Task WorldGrain_ShouldStore_PlayerCount()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Player Count World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            await grain.AddPlayerAsync("player1");
            await grain.AddPlayerAsync("player2");
            await grain.AddPlayerAsync("player3");

            var info = await grain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.PlayerCount, Is.EqualTo(3));
        }

        [Test]
        public async Task WorldGrain_ShouldRemove_Players()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Player Removal World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            await grain.AddPlayerAsync("player1");
            await grain.AddPlayerAsync("player2");
            await grain.RemovePlayerAsync("player1");

            var info = await grain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.PlayerCount, Is.EqualTo(1));
        }

        [Test]
        public async Task WorldGrain_ShouldTrack_CreationTime()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Time Tracking World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            var beforeCreate = DateTime.UtcNow;

            // Act
            await grain.InitializeAsync(config);
            var info = await grain.GetInfoAsync();

            var afterCreate = DateTime.UtcNow;

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreate.AddSeconds(-1)));
            Assert.That(info.CreatedAt, Is.LessThanOrEqualTo(afterCreate.AddSeconds(1)));
        }

        [Test]
        public async Task WorldGrain_ShouldAdd_MapToWorld()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Map Test World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            var mapId = await grain.AddMapAsync("floor-1", "maze", new System.Collections.Generic.Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();

            // Assert
            Assert.That(mapId, Is.Not.Null.And.Not.Empty);
            Assert.That(mapIds, Contains.Item(mapId));
        }

        [Test]
        public async Task WorldGrain_ShouldGet_AllMapIds()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Multi-Map World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 3 }
            };

            // Act
            await grain.InitializeAsync(config);
            var map1 = await grain.AddMapAsync("floor-1", "maze", new System.Collections.Generic.Dictionary<string, object>());
            var map2 = await grain.AddMapAsync("floor-2", "maze", new System.Collections.Generic.Dictionary<string, object>());
            var mapIds = await grain.GetMapIdsAsync();

            // Assert
            Assert.That(mapIds.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(mapIds, Contains.Item(map1));
            Assert.That(mapIds, Contains.Item(map2));
        }

        [Test]
        public async Task WorldGrain_ShouldAdd_PlayerToSpecificMap()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Player Map World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 2 }
            };

            // Act
            await grain.InitializeAsync(config);
            var mapId = await grain.AddMapAsync("main-floor", "maze", new System.Collections.Generic.Dictionary<string, object>());
            var added = await grain.AddPlayerAsync("player1", mapId);

            // Assert
            Assert.That(added, Is.True);
        }

        [Test]
        public async Task WorldGrain_ShouldMove_PlayerBetweenMaps()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Player Movement World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 2 }
            };

            // Act
            await grain.InitializeAsync(config);
            var map1 = await grain.AddMapAsync("floor-1", "maze", new System.Collections.Generic.Dictionary<string, object>());
            var map2 = await grain.AddMapAsync("floor-2", "maze", new System.Collections.Generic.Dictionary<string, object>());
            
            await grain.AddPlayerAsync("player1", map1);
            var moved = await grain.MovePlayerToMapAsync("player1", map2);
            var currentMap = await grain.GetPlayerMapAsync("player1");

            // Assert
            Assert.That(moved, Is.True);
            Assert.That(currentMap, Is.EqualTo(map2));
        }

        [Test]
        public async Task WorldGrain_ShouldReturn_NullForUnknownPlayer()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Unknown Player World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            var playerMap = await grain.GetPlayerMapAsync("nonexistent-player");

            // Assert
            Assert.That(playerMap, Is.Null);
        }

        [Test]
        public async Task WorldGrain_ShouldPreserve_WorldMetadata()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Metadata World",
                Description = "Test world with metadata",
                NarrativeId = "test-narrative-1",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            var info = await grain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.NarrativeId, Is.EqualTo("test-narrative-1"));
            Assert.That(info.Description, Is.EqualTo("Test world with metadata"));
        }

        [Test]
        public async Task WorldGrain_ShouldUpdate_LastActivityTime()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Activity World",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            };

            // Act
            await grain.InitializeAsync(config);
            var info1 = await grain.GetInfoAsync();
            
            // Simulate some activity
            await Task.Delay(100);
            await grain.AddPlayerAsync("player1");
            
            var info2 = await grain.GetInfoAsync();

            // Assert
            Assert.That(info1, Is.Not.Null);
            Assert.That(info2, Is.Not.Null);
            Assert.That(info2.LastActivityAt, Is.GreaterThanOrEqualTo(info1.LastActivityAt));
        }
    }
}
