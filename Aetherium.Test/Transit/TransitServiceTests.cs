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
using Aetherium.Server.Transit;
using Aetherium.Model.Vehicles;
using Aetherium.Model.Transit;
using Aetherium.Test.MultiWorld;

namespace Aetherium.Test.Transit
{
    /// <summary>
    /// Rideable transit (add-transit-networks Phase 3), built on the boardable-vehicles voyage machinery:
    /// a <see cref="TransitServiceGrain"/> parks a train (a <see cref="VehicleGrain"/>) at the first stop,
    /// then walks it stop-to-stop — dwelling, then departing on a timed voyage — looping the line. The
    /// headline case is a player who boards the parked train at one station and, after the service runs,
    /// alights at the next: the whole ride is the train's existing depart/arrive + board/disembark, driven
    /// by the service. Uses the Orleans TestingHost harness (mirrors <c>VehicleBoardingTests</c>).
    /// </summary>
    [TestFixture]
    public class TransitServiceTests
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

        private async Task<(string worldId, string mapId)> CreateSurfaceAsync()
        {
            var worldId = $"surface-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Transit Surface",
                Size = new WorldSize { Width = 48, Height = 48, Depth = 1 },
                GeneratorType = "maze",
            });
            var mapId = (await world.GetMapIdsAsync()).First();
            return (worldId, mapId);
        }

        /// <summary>Finds <paramref name="n"/> distinct passable, unoccupied tiles on the map to use as
        /// station docks (surveying with joins, then freeing them — like a spawn probe).</summary>
        private async Task<List<(int x, int y, int z)>> SurveyDocksAsync(string mapId, int n)
        {
            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var surveyors = new List<string>();
            var docks = new List<(int, int, int)>();
            for (int i = 0; i < n; i++)
            {
                var sv = $"surveyor-{Guid.NewGuid()}";
                var res = await map.JoinPlayerAsync(sv);
                Assert.That(res.Success, Is.True, "need a passable tile for a dock");
                var sp = res.SpawnLocation();
                docks.Add((sp.X, sp.Y, sp.Z));
                surveyors.Add(sv);
            }
            foreach (var sv in surveyors)
                await map.LeavePlayerAsync(sv); // free the dock tiles again
            return docks;
        }

        private static VehicleConfig Train(int capacity = 4) => new()
        {
            VehicleId = "test-train",
            DisplayName = "Test Train",
            FootprintWidth = 1,
            FootprintLength = 1,
            FootprintDepth = 1,
            InteriorGenerator = "maze",
            InteriorWidth = 20,
            InteriorHeight = 20,
            InteriorSeed = 7,
            Capacity = capacity,
        };

        private async Task<(ITransitServiceGrain service, string worldId, string mapId, List<(int x, int y, int z)> docks)>
            StartServiceAsync(int stops = 2, bool loop = true, int capacity = 4)
        {
            var (worldId, mapId) = await CreateSurfaceAsync();
            var docks = await SurveyDocksAsync(mapId, stops);

            var config = new TransitServiceConfig
            {
                LineId = "test-rail",
                DisplayName = "Test Rail",
                Stops = docks.Select((d, i) => new TransitStop
                {
                    DockWorldId = worldId,
                    DockMapId = mapId,
                    AnchorX = d.x,
                    AnchorY = d.y,
                    AnchorZ = d.z,
                    Name = $"Stop{i}",
                }).ToList(),
                HopMinutes = 0.0,   // deterministic: ETA is immediate, one tick arrives
                DwellMinutes = 0.0, // deterministic: departs on the next step
                Loop = loop,
                Train = Train(capacity),
            };

            var service = _cluster.GrainFactory.GetGrain<ITransitServiceGrain>($"svc-{Guid.NewGuid()}");
            await service.InitializeAsync(config);
            return (service, worldId, mapId, docks);
        }

        // ----- tests -----------------------------------------------------

        [Test]
        public async Task Initialize_ParksTheTrainAtTheFirstStop()
        {
            var (service, _, mapId, _) = await StartServiceAsync(stops: 2);

            var info = await service.GetInfoAsync();
            Assert.That(info.Started, Is.True);
            Assert.That(info.StopCount, Is.EqualTo(2));
            Assert.That(info.CurrentStopIndex, Is.EqualTo(0));
            Assert.That(info.InTransit, Is.False);

            var trainId = await service.GetTrainIdAsync();
            Assert.That(trainId, Is.Not.Null.And.Not.Empty);
            var train = _cluster.GrainFactory.GetGrain<IVehicleGrain>(trainId!);
            var t = await train.GetInfoAsync();
            Assert.That(t.Landed, Is.True, "the train starts parked at the first station");
            Assert.That(t.DockMapId, Is.EqualTo(mapId));
        }

        [Test]
        public async Task Service_WalksTheLineStopToStop_AndLoops()
        {
            var (service, _, _, _) = await StartServiceAsync(stops: 2, loop: true);

            // Depart 0 -> 1.
            await service.DispatchStepAsync();
            Assert.That((await service.GetInfoAsync()).InTransit, Is.True, "the train leaves the platform");

            // Arrive at 1.
            await service.DispatchStepAsync();
            var at1 = await service.GetInfoAsync();
            Assert.That(at1.InTransit, Is.False);
            Assert.That(at1.CurrentStopIndex, Is.EqualTo(1), "the train has reached the second station");

            // Depart 1 -> 0 (loop) and arrive.
            await service.DispatchStepAsync();
            Assert.That((await service.GetInfoAsync()).InTransit, Is.True);
            await service.DispatchStepAsync();
            var back = await service.GetInfoAsync();
            Assert.That(back.CurrentStopIndex, Is.EqualTo(0), "a looping line returns to the first station");
        }

        [Test]
        public async Task BoardedPassenger_IsCarriedToTheNextStation_AndAlights()
        {
            var (service, worldId, mapId, _) = await StartServiceAsync(stops: 2, capacity: 4);
            var trainId = (await service.GetTrainIdAsync())!;
            var train = _cluster.GrainFactory.GetGrain<IVehicleGrain>(trainId);
            var dockMap = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            var interior = _cluster.GrainFactory.GetGrain<IGameMapGrain>((await train.GetInteriorMapIdAsync())!);

            // A player waiting on the platform boards the parked train.
            var player = $"rider-{Guid.NewGuid()}";
            await dockMap.JoinPlayerAsync(player);
            var board = await train.BoardAsync(new List<string> { player });
            Assert.That(board.Success, Is.True, board.Error);
            Assert.That(await interior.GetPlayersAsync(), Does.Contain(player), "the rider is aboard");
            Assert.That(await dockMap.GetPlayersAsync(), Does.Not.Contain(player), "the rider left the platform");

            // The service runs: depart stop 0, arrive stop 1. The rider travels in the interior.
            await service.DispatchStepAsync(); // depart 0 -> 1
            await service.DispatchStepAsync(); // arrive at 1
            Assert.That((await service.GetInfoAsync()).CurrentStopIndex, Is.EqualTo(1),
                "the service carried the train to the next station");
            Assert.That(await train.GetPassengersAsync(), Does.Contain(player),
                "the rider was carried along the line inside the train");

            // At the new station the rider alights onto the surface.
            var dis = await train.DisembarkAsync(new List<string> { player });
            Assert.That(dis.Success, Is.True, dis.Error);
            Assert.That(await dockMap.GetPlayersAsync(), Does.Contain(player),
                "the rider steps off at the destination station");
            Assert.That(await interior.GetPlayersAsync(), Does.Not.Contain(player));
        }

        [Test]
        public async Task NonLoopingLine_HaltsAtTheTerminus()
        {
            var (service, _, _, _) = await StartServiceAsync(stops: 2, loop: false);

            await service.DispatchStepAsync(); // depart 0 -> 1
            await service.DispatchStepAsync(); // arrive 1 (terminus)
            Assert.That((await service.GetInfoAsync()).CurrentStopIndex, Is.EqualTo(1));

            // Further steps do nothing — a non-looping line stays at its last stop.
            await service.DispatchStepAsync();
            await service.DispatchStepAsync();
            var end = await service.GetInfoAsync();
            Assert.That(end.CurrentStopIndex, Is.EqualTo(1), "the terminus is the end of the line");
            Assert.That(end.InTransit, Is.False, "the train does not depart the terminus");
        }
    }
}
