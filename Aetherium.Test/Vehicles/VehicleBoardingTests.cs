using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Vehicles;
using Aetherium.Model.Vehicles;
using Aetherium.Test.MultiWorld;

namespace Aetherium.Test.Vehicles
{
    /// <summary>
    /// Boardable vehicles — Phase 2 (interior + boarding of a parked vehicle). A <see cref="VehicleGrain"/>
    /// creates its interior map, lands its exterior footprint on a surface, boards a party into the
    /// interior (re-pointing live sessions), and disembarks them back — no travel yet. Uses the Orleans
    /// TestingHost + the <see cref="CapturingHubContext"/> harness so grain-driven session re-points are
    /// observable.
    /// </summary>
    [TestFixture]
    public class VehicleBoardingTests
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

        // ----- helpers ---------------------------------------------------

        /// <summary>Creates a walkable surface world+map to use as a landing dock. Returns (worldId, mapId).</summary>
        private async Task<(string worldId, string mapId)> CreateSurfaceAsync()
        {
            var worldId = $"surface-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Dock Surface",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
            });
            var mapId = (await world.GetMapIdsAsync()).First();
            return (worldId, mapId);
        }

        private static VehicleConfig Config(int capacity = 8) => new()
        {
            VehicleId = "test-shuttle",
            DisplayName = "Test Shuttle",
            FootprintWidth = 1,
            FootprintLength = 1,
            FootprintDepth = 1,
            InteriorGenerator = "maze",
            InteriorWidth = 20,
            InteriorHeight = 20,
            InteriorSeed = 7,
            Capacity = capacity,
        };

        /// <summary>Lands a freshly-initialized vehicle on a valid tile of the given dock map (found by
        /// probing with a surveyor join, which guarantees a passable, unoccupied cell).</summary>
        private async Task<IVehicleGrain> InitAndLandAsync(string dockWorldId, string dockMapId, int capacity = 8)
        {
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config(capacity));

            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(dockMapId);
            var surveyor = $"surveyor-{Guid.NewGuid()}";
            var survey = await dockMap.JoinPlayerAsync(surveyor);
            Assert.That(survey.Success, Is.True, "need a passable tile to land on");
            var spawn = survey.SpawnLocation();
            await dockMap.LeavePlayerAsync(surveyor); // free the tile

            var land = await vehicle.LandAsync(dockWorldId, dockMapId, spawn.X, spawn.Y, spawn.Z);
            Assert.That(land.Success, Is.True, land.Error);
            return vehicle;
        }

        // ----- tests -----------------------------------------------------

        [Test]
        public async Task Initialize_CreatesInteriorMap()
        {
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config());

            var interiorMapId = await vehicle.GetInteriorMapIdAsync();
            Assert.That(interiorMapId, Is.Not.Null.And.Not.Empty, "InitializeAsync must create an interior map");

            // The interior map is a real, joinable map.
            var interior = _cluster.GrainFactory.GetGrain<IGameMapGrain>(interiorMapId!);
            var probe = await interior.JoinPlayerAsync($"probe-{Guid.NewGuid()}");
            Assert.That(probe.Success, Is.True, "the interior must be a walkable, joinable map");
        }

        [Test]
        public async Task Board_MovesPlayerIntoInterior_AndOffTheSurface()
        {
            var (dockWorldId, dockMapId) = await CreateSurfaceAsync();
            var vehicle = await InitAndLandAsync(dockWorldId, dockMapId);
            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(dockMapId);
            var interior = _cluster.GrainFactory.GetGrain<IGameMapGrain>((await vehicle.GetInteriorMapIdAsync())!);

            var player = $"player-{Guid.NewGuid()}";
            await dockMap.JoinPlayerAsync(player); // player standing on the surface near the ship

            var board = await vehicle.BoardAsync(new List<string> { player });
            Assert.That(board.Success, Is.True, board.Error);
            Assert.That(board.Moved, Is.EqualTo(1));

            Assert.That(await interior.GetPlayersAsync(), Does.Contain(player), "player must be inside the interior");
            Assert.That(await dockMap.GetPlayersAsync(), Does.Not.Contain(player), "player must have left the surface");
            Assert.That(await vehicle.GetPassengersAsync(), Does.Contain(player));
        }

        [Test]
        public async Task Disembark_ReturnsPlayerToTheSurface()
        {
            var (dockWorldId, dockMapId) = await CreateSurfaceAsync();
            var vehicle = await InitAndLandAsync(dockWorldId, dockMapId);
            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(dockMapId);
            var interior = _cluster.GrainFactory.GetGrain<IGameMapGrain>((await vehicle.GetInteriorMapIdAsync())!);

            var player = $"player-{Guid.NewGuid()}";
            await dockMap.JoinPlayerAsync(player);
            await vehicle.BoardAsync(new List<string> { player });

            var dis = await vehicle.DisembarkAsync(new List<string> { player });
            Assert.That(dis.Success, Is.True, dis.Error);
            Assert.That(dis.Moved, Is.EqualTo(1));

            Assert.That(await dockMap.GetPlayersAsync(), Does.Contain(player), "player must be back on the surface");
            Assert.That(await interior.GetPlayersAsync(), Does.Not.Contain(player), "player must have left the interior");
            Assert.That(await vehicle.GetPassengersAsync(), Does.Not.Contain(player));
        }

        [Test]
        public async Task Board_RejectsSurplusOverCapacity()
        {
            var (dockWorldId, dockMapId) = await CreateSurfaceAsync();
            var vehicle = await InitAndLandAsync(dockWorldId, dockMapId, capacity: 1);
            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(dockMapId);
            var interior = _cluster.GrainFactory.GetGrain<IGameMapGrain>((await vehicle.GetInteriorMapIdAsync())!);

            var p1 = $"p1-{Guid.NewGuid()}";
            var p2 = $"p2-{Guid.NewGuid()}";
            await dockMap.JoinPlayerAsync(p1);
            await dockMap.JoinPlayerAsync(p2);

            var board = await vehicle.BoardAsync(new List<string> { p1, p2 });
            Assert.That(board.Moved, Is.EqualTo(1), "only capacity-many may board");
            Assert.That(board.Rejected, Is.GreaterThanOrEqualTo(1), "the surplus must be rejected");
            Assert.That((await interior.GetPlayersAsync()).Count, Is.EqualTo(1), "the interior must not exceed capacity");
        }

        [Test]
        public async Task Board_RequiresLandedVehicle()
        {
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config()); // initialized but never landed

            var board = await vehicle.BoardAsync(new List<string> { $"player-{Guid.NewGuid()}" });
            Assert.That(board.Success, Is.False, "boarding an un-landed (in-transit) vehicle must be rejected");
            Assert.That(board.Moved, Is.EqualTo(0));
        }

        [Test]
        public async Task Board_RepointsALiveSessionIntoTheInterior()
        {
            var (dockWorldId, dockMapId) = await CreateSurfaceAsync();
            var vehicle = await InitAndLandAsync(dockWorldId, dockMapId);
            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(dockMapId);
            var interiorMapId = (await vehicle.GetInteriorMapIdAsync())!;

            var player = $"player-{Guid.NewGuid()}";
            await dockMap.JoinPlayerAsync(player);

            // A live session bound to the dock map, with a working World mirror so GetPerception runs.
            var session = new GameSession("conn-V", new Aetherium.WorldBuilders.FovDiagnosticWorldBuilder("open_space"))
            {
                SessionId = player,
                ConnectionId = "conn-V",
                WorldId = dockWorldId,
                MapId = dockMapId,
            };
            InjectSession(session);

            _hubContext.Reset();
            var board = await vehicle.BoardAsync(new List<string> { player });
            Assert.That(board.Success, Is.True, board.Error);

            Assert.That(session.MapId, Is.EqualTo(interiorMapId), "boarding must re-point the session onto the interior map");
            var frames = _hubContext.GetDispatches()
                .Where(d => d.Method == "ReceivePerceptionUpdate" && d.ConnectionId == "conn-V")
                .ToList();
            Assert.That(frames.Count, Is.GreaterThanOrEqualTo(1), "boarding must push a fresh interior perception frame");
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
