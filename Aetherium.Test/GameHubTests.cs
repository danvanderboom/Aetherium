using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using global::Orleans.Configuration;
using Orleans;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Management;
using Aetherium.Components;

namespace Aetherium.Test
{
    [TestFixture]
    public class GameHubTests
    {
        private TestCluster _cluster = null!;
        private GameSessionManager _sessionManager = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Orleans 9 uses source generators for automatic grain discovery from referenced assemblies
                
                // Configure test grain storage
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");

                // Increase request timeout to accommodate world generation during tests
                siloBuilder.Configure<SiloMessagingOptions>(opts =>
                {
                    opts.ResponseTimeout = TimeSpan.FromMinutes(3);
                });

                // Register required services used by grains
                siloBuilder.ConfigureServices(services =>
                {
                    // Core simulation options for tests (use large region to minimize region grains)
                    services.Configure<Aetherium.Server.Simulation.SimulationOptions>(opts =>
                    {
                        opts.RegionSize = 128;
                        opts.EnableWeather = false;
                        opts.EnableSeasons = false;
                        opts.EnableAgentChanges = false;
                        opts.EnableProceduralEvents = false;
                    });

                    // In-memory snapshot store for regions (required dependency)
                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore, Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

                    // Map generator registry
                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });

                    // Session manager for GameHub
                    services.AddSingleton<Aetherium.Server.GameSessionManager>();

                    // No-op HubContext for tests
                    services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<Aetherium.Server.GameHub>>(sp => new NullHubContext());
                });
            }

            private sealed class NullHubContext : Microsoft.AspNetCore.SignalR.IHubContext<Aetherium.Server.GameHub>
            {
                public Microsoft.AspNetCore.SignalR.IHubClients Clients { get; } = new NullHubClients();
                public Microsoft.AspNetCore.SignalR.IGroupManager Groups { get; } = new NullGroupManager();

                private sealed class NullHubClients : Microsoft.AspNetCore.SignalR.IHubClients
                {
                    public Microsoft.AspNetCore.SignalR.IClientProxy All => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Clients(System.Collections.Generic.IReadOnlyList<string> connectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Groups(System.Collections.Generic.IReadOnlyList<string> groupNames) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Users(System.Collections.Generic.IReadOnlyList<string> userIds) => new NullClientProxy();
                }

                private sealed class NullGroupManager : Microsoft.AspNetCore.SignalR.IGroupManager
                {
                    public System.Threading.Tasks.Task AddToGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                    public System.Threading.Tasks.Task RemoveFromGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                }

                private sealed class NullClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
                {
                    public System.Threading.Tasks.Task SendCoreAsync(string method, object?[] args, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                }
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

        [SetUp]
        public void SetUp()
        {
            _sessionManager = new GameSessionManager();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task GameHub_ShouldCreate_NewWorld()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Hub Test World",
                Description = "Test world via hub",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            var worldId = await managementGrain.CreateWorldAsync(request);

            // Assert
            Assert.That(worldId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GameHub_ShouldList_Worlds()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            
            var request1 = new CreateWorldRequest
            {
                Name = "World 1",
                Description = "First world",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };
            var request2 = new CreateWorldRequest
            {
                Name = "World 2",
                Description = "Second world",
                Size = new WorldSize { Width = 60, Height = 60, Depth = 1 }
            };

            await managementGrain.CreateWorldAsync(request1);
            await managementGrain.CreateWorldAsync(request2);

            // Act
            var worlds = await managementGrain.ListWorldsAsync();

            // Assert
            Assert.That(worlds, Is.Not.Null);
            Assert.That(worlds.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GameHub_ShouldGet_WorldInfo()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Info Hub World",
                Description = "Testing world info",
                MaxPlayers = 20,
                Size = new WorldSize { Width = 80, Height = 80, Depth = 1 }
            };

            var worldId = await managementGrain.CreateWorldAsync(request);

            // Act
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.Name, Is.EqualTo("Info Hub World"));
            Assert.That(info.Description, Is.EqualTo("Testing world info"));
            Assert.That(info.MaxPlayers, Is.EqualTo(20));
        }

        [Test]
        public async Task GameHub_ShouldHandle_InvalidWorldId()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var invalidWorldId = $"nonexistent-world-{Guid.NewGuid()}";

            // Act
            var info = await managementGrain.GetWorldInfoAsync(invalidWorldId);

            // Assert
            Assert.That(info, Is.Null);
        }

        [Test]
        public async Task GameHub_ShouldCreate_WorldWithMapGrain()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var worldId = $"test-world-{Guid.NewGuid()}";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>($"{worldId}-map-1");
            
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Map Test World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            await worldGrain.InitializeAsync(config);
            await mapGrain.InitializeAsync(worldId, "floor-1", config.Size, "maze", new Dictionary<string, object>());

            // Assert
            var worldInfo = await worldGrain.GetInfoAsync();
            var mapMetadata = await mapGrain.GetMetadataAsync();
            
            Assert.That(worldInfo, Is.Not.Null);
            Assert.That(mapMetadata, Is.Not.Null);
            Assert.That(mapMetadata.WorldId, Is.EqualTo(worldId));
        }

        [Test]
        public async Task GameHub_ShouldRegister_GameSession()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            // Act
            await managementGrain.RegisterSessionAsync(sessionId, connectionId);
            var sessions = await managementGrain.ListSessionsAsync();

            // Assert
            Assert.That(sessions.Any(s => s.SessionId == sessionId), Is.True);
        }

        [Test]
        public async Task GameHub_ShouldUnregister_GameSession()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            await managementGrain.RegisterSessionAsync(sessionId, connectionId);

            // Act
            await managementGrain.UnregisterSessionAsync(sessionId);
            var sessions = await managementGrain.ListSessionsAsync();

            // Assert
            Assert.That(sessions.Any(s => s.SessionId == sessionId), Is.False);
        }

        [Test]
        public async Task GameHub_ShouldCheck_WorldState()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "State Check World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            await worldGrain.InitializeAsync(config);
            var state = await worldGrain.GetStateAsync();

            // Assert
            Assert.That(state, Is.EqualTo(WorldState.Active).Or.EqualTo(WorldState.Creating));
        }

        [Test]
        public async Task GameHub_ShouldAllow_MultipleSessionsInWorld()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");

            var sessionId1 = $"session-{Guid.NewGuid()}";
            var sessionId2 = $"session-{Guid.NewGuid()}";
            var sessionId3 = $"session-{Guid.NewGuid()}";

            // Act
            await managementGrain.RegisterSessionAsync(sessionId1, "conn-1");
            await managementGrain.RegisterSessionAsync(sessionId2, "conn-2");
            await managementGrain.RegisterSessionAsync(sessionId3, "conn-3");

            var sessions = await managementGrain.ListSessionsAsync();

            // Assert
            Assert.That(sessions.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task GameHub_ShouldTransition_WorldStates()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            
            await worldGrain.InitializeAsync(new WorldConfig 
            { 
                WorldId = worldId,
                Name = "State Transition World", 
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 } 
            });

            // Act & Assert - Pause
            await worldGrain.PauseAsync();
            var state1 = await worldGrain.GetStateAsync();
            Assert.That(state1, Is.EqualTo(WorldState.Paused));

            // Act & Assert - Resume
            await worldGrain.ResumeAsync();
            var state2 = await worldGrain.GetStateAsync();
            Assert.That(state2, Is.EqualTo(WorldState.Active));
        }

        [Test]
        public async Task GameHub_ShouldTrack_PlayerCount()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            
            await worldGrain.InitializeAsync(new WorldConfig 
            { 
                WorldId = worldId,
                Name = "Player Count World", 
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 } 
            });

            // Act
            await worldGrain.AddPlayerAsync("player1");
            await worldGrain.AddPlayerAsync("player2");
            await worldGrain.AddPlayerAsync("player3");

            var info = await worldGrain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.PlayerCount, Is.EqualTo(3));
        }

        [Test]
        public async Task GameHub_ShouldUpdate_PlayerCount_OnRemove()
        {
            // Arrange
            var worldId = $"test-world-{Guid.NewGuid()}";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            
            await worldGrain.InitializeAsync(new WorldConfig 
            { 
                WorldId = worldId,
                Name = "Remove Player World", 
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 } 
            });
            await worldGrain.AddPlayerAsync("player1");
            await worldGrain.AddPlayerAsync("player2");
            await worldGrain.AddPlayerAsync("player3");

            // Act
            await worldGrain.RemovePlayerAsync("player2");
            var info = await worldGrain.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GameHub_ShouldPersist_WorldConfiguration()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Persist Test World",
                Description = "Testing persistence",
                MaxPlayers = 50,
                Size = new WorldSize { Width = 100, Height = 100, Depth = 1 }
            };

            // Act
            var worldId = await managementGrain.CreateWorldAsync(request);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.Name, Is.EqualTo("Persist Test World"));
            Assert.That(info.Description, Is.EqualTo("Testing persistence"));
            Assert.That(info.MaxPlayers, Is.EqualTo(50));
        }

        [Test]
        public async Task GameHub_ShouldCreate_WorldWithNarrative()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Narrative World",
                NarrativeId = "test-narrative",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            var worldId = await managementGrain.CreateWorldAsync(request);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.NarrativeId, Is.EqualTo("test-narrative"));
        }

        [Test]
        public async Task GameHub_ShouldGet_SessionByConnectionId()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            await managementGrain.RegisterSessionAsync(sessionId, connectionId);

            // Act
            var sessionInfo = await managementGrain.GetSessionByConnectionIdAsync(connectionId);

            // Assert
            Assert.That(sessionInfo, Is.Not.Null);
            Assert.That(sessionInfo.SessionId, Is.EqualTo(sessionId));
            Assert.That(sessionInfo.ConnectionId, Is.EqualTo(connectionId));
        }
    }
}

