using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
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
    /// Boardable vehicles — Phase 3 (timed voyage between worlds). Departure removes the exterior from
    /// the origin surface and starts a timed journey; arrival places the exterior at the destination dock
    /// and re-lands. The voyage step (<see cref="IVehicleGrain.TickVoyageAsync"/>) is driven directly for
    /// deterministic timing (the Orleans reminder that self-drives it in production has a 1-minute floor).
    /// </summary>
    [TestFixture]
    public class VehicleVoyageTests
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
                // Exercise the reminder-arming path in DepartAsync/ArriveAsync (the voyage itself is
                // driven directly via TickVoyageAsync, so the 1-minute reminder floor doesn't slow tests).
                siloBuilder.UseInMemoryReminderService();

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

        // ----- helpers ---------------------------------------------------

        private async Task<(string worldId, string mapId)> CreateSurfaceAsync(string label)
        {
            var worldId = $"{label}-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = label,
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 },
                GeneratorType = "maze",
            });
            var mapId = (await world.GetMapIdsAsync()).First();
            return (worldId, mapId);
        }

        /// <summary>Probes a map for a passable, unoccupied cell (a valid landing tile).</summary>
        private async Task<(int x, int y, int z)> FindLandingTileAsync(string mapId)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var surveyor = $"surveyor-{Guid.NewGuid()}";
            var survey = await map.JoinPlayerAsync(surveyor);
            Assert.That(survey.Success, Is.True);
            var spawn = survey.SpawnLocation();
            await map.LeavePlayerAsync(surveyor);
            return (spawn.X, spawn.Y, spawn.Z);
        }

        private static VehicleConfig Config() => new()
        {
            VehicleId = "voyager",
            DisplayName = "Voyager",
            FootprintWidth = 1,
            FootprintLength = 1,
            FootprintDepth = 1,
            InteriorGenerator = "maze",
            InteriorWidth = 20,
            InteriorHeight = 20,
            InteriorSeed = 11,
            Capacity = 8,
        };

        // ----- tests -----------------------------------------------------

        [Test]
        public async Task Depart_RemovesOriginExterior_MarksInTransit()
        {
            var (originWorld, originMap) = await CreateSurfaceAsync("origin");
            var (destWorld, destMap) = await CreateSurfaceAsync("dest");

            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config());
            var (ox, oy, oz) = await FindLandingTileAsync(originMap);
            Assert.That((await vehicle.LandAsync(originWorld, originMap, ox, oy, oz)).Success, Is.True);

            var (dx, dy, dz) = await FindLandingTileAsync(destMap);
            var depart = await vehicle.DepartAsync(destWorld, destMap, dx, dy, dz, voyageMinutes: 60);
            Assert.That(depart.Success, Is.True, depart.Error);

            var info = await vehicle.GetInfoAsync();
            Assert.That(info.InTransit, Is.True, "vehicle must be in transit after departure");
            Assert.That(info.Landed, Is.False);

            // The exterior is gone from the origin surface.
            var originMapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(originMap);
            var atOrigin = await originMapGrain.GetBoardableInfoAsync("nobody", $"vehicle:{vehicle.GetPrimaryKeyString()}");
            Assert.That(atOrigin.Found, Is.False, "the exterior must be removed from the origin on takeoff");
        }

        [Test]
        public async Task Voyage_StaysInTransit_BeforeEta()
        {
            var (originWorld, originMap) = await CreateSurfaceAsync("origin");
            var (destWorld, destMap) = await CreateSurfaceAsync("dest");
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config());
            var (ox, oy, oz) = await FindLandingTileAsync(originMap);
            await vehicle.LandAsync(originWorld, originMap, ox, oy, oz);
            var (dx, dy, dz) = await FindLandingTileAsync(destMap);
            await vehicle.DepartAsync(destWorld, destMap, dx, dy, dz, voyageMinutes: 60);

            await vehicle.TickVoyageAsync(); // well before the 60-minute ETA

            var info = await vehicle.GetInfoAsync();
            Assert.That(info.InTransit, Is.True, "a wake before the ETA must not arrive");
            var destMapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(destMap);
            var atDest = await destMapGrain.GetBoardableInfoAsync("nobody", $"vehicle:{vehicle.GetPrimaryKeyString()}");
            Assert.That(atDest.Found, Is.False, "the exterior must not appear at the destination before arrival");
        }

        [Test]
        public async Task Voyage_ArrivesAtEta_ReDocksAtDestination_AndPassengerCanDisembark()
        {
            var (originWorld, originMap) = await CreateSurfaceAsync("origin");
            var (destWorld, destMap) = await CreateSurfaceAsync("dest");
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config());
            var interiorMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>((await vehicle.GetInteriorMapIdAsync())!);

            var (ox, oy, oz) = await FindLandingTileAsync(originMap);
            await vehicle.LandAsync(originWorld, originMap, ox, oy, oz);

            // A passenger boards at the origin and should travel with the ship.
            var originMapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(originMap);
            var player = $"player-{Guid.NewGuid()}";
            await originMapGrain.JoinPlayerAsync(player);
            Assert.That((await vehicle.BoardAsync(new List<string> { player })).Moved, Is.EqualTo(1));

            var (dx, dy, dz) = await FindLandingTileAsync(destMap);
            await vehicle.DepartAsync(destWorld, destMap, dx, dy, dz, voyageMinutes: 0); // ETA = now

            // The passenger stays in the interior en route.
            Assert.That(await interiorMap.GetPlayersAsync(), Does.Contain(player));

            await vehicle.TickVoyageAsync(); // ETA has passed -> arrive

            var info = await vehicle.GetInfoAsync();
            Assert.That(info.InTransit, Is.False, "arrival must clear in-transit");
            Assert.That(info.Landed, Is.True, "the vehicle must be landed at the destination");
            Assert.That(info.DockMapId, Is.EqualTo(destMap));

            var destMapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(destMap);
            var atDest = await destMapGrain.GetBoardableInfoAsync("nobody", $"vehicle:{vehicle.GetPrimaryKeyString()}");
            Assert.That(atDest.Found, Is.True, "the exterior must be placed at the destination dock");

            // Disembark onto the destination surface.
            var dis = await vehicle.DisembarkAsync(new List<string> { player });
            Assert.That(dis.Moved, Is.EqualTo(1), dis.Error);
            Assert.That(await destMapGrain.GetPlayersAsync(), Does.Contain(player), "passenger must arrive on the destination surface");
            Assert.That(await interiorMap.GetPlayersAsync(), Does.Not.Contain(player));
        }

        [Test]
        public async Task Depart_RequiresLandedVehicle()
        {
            var (destWorld, destMap) = await CreateSurfaceAsync("dest");
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(Config()); // never landed

            var depart = await vehicle.DepartAsync(destWorld, destMap, 1, 1, 0, voyageMinutes: 10);
            Assert.That(depart.Success, Is.False, "an un-landed vehicle cannot depart");
        }

        [Test]
        public async Task InTransitEvent_FiresAndBroadcastsToPassengers()
        {
            var (originWorld, originMap) = await CreateSurfaceAsync("origin");
            var (destWorld, destMap) = await CreateSurfaceAsync("dest");

            // A vehicle whose voyage schedules an encounter right at departure (offset 0).
            var cfg = Config();
            cfg.InTransitEvents = new List<VoyageEventDef>
            {
                new() { OffsetMinutes = 0, EventType = "asteroid_field", Description = "Asteroids ahead!" }
            };
            var vehicle = _cluster.GrainFactory.GetGrain<IVehicleGrain>($"veh-{Guid.NewGuid()}");
            await vehicle.InitializeAsync(cfg);
            var (ox, oy, oz) = await FindLandingTileAsync(originMap);
            await vehicle.LandAsync(originWorld, originMap, ox, oy, oz);

            // A passenger with a live session boards at the origin.
            var originMapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(originMap);
            var player = $"player-{Guid.NewGuid()}";
            await originMapGrain.JoinPlayerAsync(player);
            var session = new GameSession("conn-E", new Aetherium.WorldBuilders.FovDiagnosticWorldBuilder("open_space"))
            {
                SessionId = player,
                ConnectionId = "conn-E",
                WorldId = originWorld,
                MapId = originMap,
            };
            InjectSession(session);
            Assert.That((await vehicle.BoardAsync(new List<string> { player })).Moved, Is.EqualTo(1));

            // Depart on a long voyage so the vehicle stays in transit; the offset-0 event is already due.
            var (dx, dy, dz) = await FindLandingTileAsync(destMap);
            await vehicle.DepartAsync(destWorld, destMap, dx, dy, dz, voyageMinutes: 60);

            _hubContext.Reset();
            await vehicle.TickVoyageAsync(); // fires the due in-transit event

            var events = _hubContext.GetDispatches()
                .Where(d => d.Method == "ReceiveVoyageEvent" && d.ConnectionId == "conn-E")
                .ToList();
            Assert.That(events.Count, Is.EqualTo(1), "the due in-transit event must be broadcast to everyone aboard");
            Assert.That((await vehicle.GetInfoAsync()).InTransit, Is.True, "the interior keeps travelling after the event");

            // The event fires at most once.
            _hubContext.Reset();
            await vehicle.TickVoyageAsync();
            var again = _hubContext.GetDispatches().Count(d => d.Method == "ReceiveVoyageEvent" && d.ConnectionId == "conn-E");
            Assert.That(again, Is.EqualTo(0), "an already-fired in-transit event must not re-broadcast");
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
