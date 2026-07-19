using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.SignalR;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Phase 0 of add-boardable-vehicles — the session → world/map perception re-point. Verifies the
    /// two new building blocks the vehicle boarding/voyage phases depend on:
    /// <list type="bullet">
    /// <item><see cref="IWorldGrain.RegisterPlayerLocationAsync"/> / <see cref="IWorldGrain.UnregisterPlayerAsync"/>
    /// keep the world grain's player-location record (the "which map is this player on" source of truth)
    /// in agreement with a re-point performed against the map grains, without inflating the player count.</item>
    /// <item><see cref="GameSessionManager.RepointSessionAsync"/> swaps a live session onto a target map,
    /// rebinds its world/map, and pushes a fresh perception frame — after which the session's perception
    /// follows the new map (a mutation there reaches the connection).</item>
    /// </list>
    /// Reuses the <see cref="CapturingHubContext"/> harness from EndToEndSharedMutationTests.
    /// </summary>
    [TestFixture]
    public class SessionRepointTests
    {
        private TestCluster _cluster = null!;
        private static CapturingHubContext _hubContext = null!;
        private static GameSessionManager _sessionManager = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");

                siloBuilder.Configure<SiloMessagingOptions>(opts => opts.ResponseTimeout = TimeSpan.FromMinutes(3));

                siloBuilder.ConfigureServices(services =>
                {
                    services.Configure<Aetherium.Server.Simulation.SimulationOptions>(opts =>
                    {
                        opts.RegionSize = 128;
                        opts.EnableWeather = false;
                        opts.EnableSeasons = false;
                        opts.EnableAgentChanges = false;
                        opts.EnableProceduralEvents = false;
                    });

                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore,
                        Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });

                    services.AddSingleton<IHubContext<GameHub>>(_hubContext);
                    services.AddSingleton<GameSessionManager>(sp => _sessionManager);
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _hubContext = new CapturingHubContext();
            _sessionManager = new GameSessionManager(perceptionService: null, hubContext: _hubContext);

            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void SetUp() => _hubContext.Reset();

        private static readonly Aetherium.WorldGen.MapGeneratorRegistry _clientRegistry = BuildRegistry();

        private static Aetherium.WorldGen.MapGeneratorRegistry BuildRegistry()
        {
            var reg = new Aetherium.WorldGen.MapGeneratorRegistry();
            reg.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
            return reg;
        }

        // ----- Source-of-truth invariant --------------------------------

        [Test]
        public async Task WorldGrain_RegisterAndUnregister_KeepPlayerLocationInAgreement()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Repoint Invariant World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
            });
            var map1 = (await world.GetMapIdsAsync()).First();
            var map2 = await world.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());

            var player = $"player-{Guid.NewGuid()}";

            // Not tracked yet.
            Assert.That(await world.GetPlayerMapAsync(player), Is.Null);
            var infoBefore = await world.GetInfoAsync();
            var countBefore = infoBefore!.PlayerCount;

            // Register on map1: location recorded, count bumped once.
            await world.RegisterPlayerLocationAsync(player, map1);
            Assert.That(await world.GetPlayerMapAsync(player), Is.EqualTo(map1));
            Assert.That((await world.GetInfoAsync())!.PlayerCount, Is.EqualTo(countBefore + 1));

            // Re-point to map2: location follows, count unchanged (idempotent).
            await world.RegisterPlayerLocationAsync(player, map2);
            Assert.That(await world.GetPlayerMapAsync(player), Is.EqualTo(map2));
            Assert.That((await world.GetInfoAsync())!.PlayerCount, Is.EqualTo(countBefore + 1),
                "re-pointing an already-tracked player must not inflate the player count");

            // Unregister: location cleared, count restored.
            await world.UnregisterPlayerAsync(player);
            Assert.That(await world.GetPlayerMapAsync(player), Is.Null);
            Assert.That((await world.GetInfoAsync())!.PlayerCount, Is.EqualTo(countBefore));
        }

        // ----- Session re-point -----------------------------------------

        [Test]
        public async Task RepointSession_SwitchesMapAndPushesFreshFrame()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Repoint Session World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
            });
            var map1Id = (await world.GetMapIdsAsync()).First();
            var map2Id = await world.AddMapAsync("floor-2", "maze", new Dictionary<string, object>());
            var map2 = _cluster.GrainFactory.GetGrain<IGameMapGrain>(map2Id);

            var player = $"player-{Guid.NewGuid()}";

            // A live session currently bound to map1, with a working World mirror so GetPerception runs.
            var session = new GameSession("conn-A", new FovDiagnosticWorldBuilder("open_space"))
            {
                SessionId = player,
                ConnectionId = "conn-A",
                WorldId = worldId,
                MapId = map1Id,
            };
            InjectSession(session);

            // Join the target map to get the canonical Character + spawn + a joiner snapshot, exactly
            // as GameHub.RepointCallerToMapAsync does before calling RepointSessionAsync.
            var join2 = await map2.JoinPlayerAsync(player);
            Assert.That(join2.Success, Is.True);
            var snapshot2 = await map2.GetWorldSnapshotForJoinerAsync(player);
            var builder2 = new SnapshotWorldBuilder(snapshot2, _clientRegistry);

            _hubContext.Reset();
            var ok = await _sessionManager.RepointSessionAsync(
                player, builder2, worldId, map2Id, join2.SpawnLocation());

            Assert.That(ok, Is.True);
            Assert.That(session.MapId, Is.EqualTo(map2Id), "the session must be rebound to the target map");
            Assert.That(session.WorldId, Is.EqualTo(worldId));

            // The re-point pushed a fresh frame to the connection.
            var repointDispatches = _hubContext.GetDispatches()
                .Where(d => d.Method == "ReceivePerceptionUpdate" && d.ConnectionId == "conn-A")
                .ToList();
            Assert.That(repointDispatches.Count, Is.GreaterThanOrEqualTo(1),
                "re-pointing must push a fresh perception frame to the session's connection");

            // Perception now follows the NEW map: another player joining map2 fans out to conn-A.
            await _sessionManager.WaitForPerceptionQuiescenceAsync(TimeSpan.FromSeconds(2));
            _hubContext.Reset();
            var other = $"player-{Guid.NewGuid()}";
            await map2.JoinPlayerAsync(other);
            await _hubContext.WaitForDispatchesAsync(expectedMinCount: 1, TimeSpan.FromSeconds(2));
            var followed = _hubContext.GetDispatches()
                .Where(d => d.Method == "ReceivePerceptionUpdate" && d.ConnectionId == "conn-A")
                .ToList();
            Assert.That(followed.Count, Is.GreaterThanOrEqualTo(1),
                "after the re-point, a mutation on the target map must reach the re-pointed session");
        }

        private void InjectSession(GameSession session)
        {
            var sessionsField = typeof(GameSessionManager).GetField("sessions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var sessionsDict = (System.Collections.Concurrent.ConcurrentDictionary<string, GameSession>)
                sessionsField.GetValue(_sessionManager)!;
            sessionsDict[session.ConnectionId] = session;
        }
    }
}
