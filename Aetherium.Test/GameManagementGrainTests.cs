using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans.Configuration;
using Orleans;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;
using Aetherium.Components;

namespace Aetherium.Test
{
    [TestFixture]
    public class GameManagementGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Orleans 9 uses source generators for automatic grain discovery from referenced assemblies
                
                // Add in-memory grain storage for testing (names must match grain attributes)
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

                    // In-memory snapshot store for regions (required dependency for MapRegionGrain)
                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore, Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

                    // Map generator registry
                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });

                    // Session manager
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

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task GameManagement_ShouldCreate_NewWorld()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Test World",
                Description = "A test world",
                GeneratorType = "maze",
                MaxPlayers = 10,
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            // Act
            var worldId = await managementGrain.CreateWorldAsync(request);

            // Assert
            Assert.That(worldId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GameManagement_ShouldList_AllWorlds()
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

            var worldId1 = await managementGrain.CreateWorldAsync(request1);
            var worldId2 = await managementGrain.CreateWorldAsync(request2);

            // Act
            var worlds = await managementGrain.ListWorldsAsync();

            // Assert
            Assert.That(worlds.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(worlds.Any(w => w.WorldId == worldId1), Is.True);
            Assert.That(worlds.Any(w => w.WorldId == worldId2), Is.True);
        }

        [Test]
        public async Task GameManagement_ShouldGet_WorldInfo()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Info Test World",
                Description = "Testing world info retrieval",
                MaxPlayers = 20,
                Size = new WorldSize { Width = 80, Height = 80, Depth = 1 }
            };

            var worldId = await managementGrain.CreateWorldAsync(request);

            // Act
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info.WorldId, Is.EqualTo(worldId));
            Assert.That(info.Name, Is.EqualTo("Info Test World"));
            Assert.That(info.Description, Is.EqualTo("Testing world info retrieval"));
            Assert.That(info.MaxPlayers, Is.EqualTo(20));
        }

        [Test]
        public async Task GameManagement_ShouldPause_World()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Pause Test World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            var worldId = await managementGrain.CreateWorldAsync(request);

            // Act
            var result = await managementGrain.PauseWorldAsync(worldId);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(info, Is.Not.Null);
            Assert.That(info.State, Is.EqualTo(WorldState.Paused));
        }

        [Test]
        public async Task GameManagement_ShouldResume_PausedWorld()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Resume Test World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            var worldId = await managementGrain.CreateWorldAsync(request);
            await managementGrain.PauseWorldAsync(worldId);

            // Act
            var result = await managementGrain.ResumeWorldAsync(worldId);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(info, Is.Not.Null);
            Assert.That(info.State, Is.EqualTo(WorldState.Active));
        }

        [Test]
        public async Task GameManagement_ShouldShutdown_World()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Shutdown Test World",
                Size = new WorldSize { Width = 50, Height = 50, Depth = 1 }
            };

            var worldId = await managementGrain.CreateWorldAsync(request);

            // Act
            var result = await managementGrain.ShutdownWorldAsync(worldId);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(info, Is.Not.Null);
            Assert.That(info.State, Is.EqualTo(WorldState.ShuttingDown).Or.EqualTo(WorldState.Stopped));
        }

        [Test]
        public async Task GameManagement_ShouldReturn_NullForInvalidWorldId()
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
        public async Task GameManagement_ShouldRegister_GameSession()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            // Act
            await managementGrain.RegisterSessionAsync(sessionId, connectionId);
            var sessions = await managementGrain.ListSessionsAsync();

            // Assert
            Assert.That(sessions, Is.Not.Null);
            Assert.That(sessions.Any(s => s.SessionId == sessionId), Is.True);
        }

        [Test]
        public async Task GameManagement_ShouldUnregister_GameSession()
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
        public async Task GameManagement_ShouldGet_SessionInfo()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            await managementGrain.RegisterSessionAsync(sessionId, connectionId);

            // Act
            var sessionInfo = await managementGrain.GetSessionInfoAsync(sessionId);

            // Assert
            Assert.That(sessionInfo, Is.Not.Null);
            Assert.That(sessionInfo.SessionId, Is.EqualTo(sessionId));
            Assert.That(sessionInfo.ConnectionId, Is.EqualTo(connectionId));
        }

        [Test]
        public async Task GameManagement_ShouldGet_SessionByConnectionId()
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

        [Test]
        public async Task GameManagement_ShouldCount_ActiveSessions()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId1 = $"session-{Guid.NewGuid()}";
            var sessionId2 = $"session-{Guid.NewGuid()}";
            var sessionId3 = $"session-{Guid.NewGuid()}";

            await managementGrain.RegisterSessionAsync(sessionId1, "conn-1");
            await managementGrain.RegisterSessionAsync(sessionId2, "conn-2");
            await managementGrain.RegisterSessionAsync(sessionId3, "conn-3");

            // Act
            var count = await managementGrain.GetSessionCountAsync();

            // Assert
            Assert.That(count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task GameManagement_ShouldTerminate_Session()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var sessionId = $"session-{Guid.NewGuid()}";
            var connectionId = $"connection-{Guid.NewGuid()}";

            await managementGrain.RegisterSessionAsync(sessionId, connectionId);

            // Act
            var result = await managementGrain.TerminateSessionAsync(sessionId);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task GameManagement_ShouldCreate_WorldWithNarrative()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Narrative World",
                Description = "World with narrative",
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
        public async Task GameManagement_ShouldCreate_WorldWithCustomGenerator()
        {
            // Arrange
            var managementGrain = _cluster.GrainFactory.GetGrain<IGameManagementGrain>("main");
            var request = new CreateWorldRequest
            {
                Name = "Custom Generator World",
                GeneratorType = "city",
                GeneratorParameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["blockSize"] = 12,
                    ["streetWidth"] = 2
                },
                Size = new WorldSize { Width = 100, Height = 100, Depth = 1 }
            };

            // Act
            var worldId = await managementGrain.CreateWorldAsync(request);
            var info = await managementGrain.GetWorldInfoAsync(worldId);

            // Assert
            Assert.That(worldId, Is.Not.Null.And.Not.Empty);
            Assert.That(info, Is.Not.Null);
            Assert.That(info.Name, Is.EqualTo("Custom Generator World"));
        }
    }
}

