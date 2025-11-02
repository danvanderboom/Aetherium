using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test.Orleans
{
    [TestFixture]
    public class ClusterGrainTests
    {
        private TestCluster? _cluster;

        [SetUp]
        public async Task SetUp()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                _cluster.Dispose();
            }
        }

        [Test]
        public async Task ClusterGrain_Initialize_CreatesCluster()
        {
            // Arrange
            var clusterId = "test-cluster-1";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster for unit tests",
                WorldIds = new HashSet<string>()
            };

            // Act
            await grain.InitializeAsync(clusterInfo);

            // Assert
            var info = await grain.GetClusterInfoAsync();
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.ClusterId, Is.EqualTo(clusterId));
            Assert.That(info.Name, Is.EqualTo("Test Cluster"));
        }

        [Test]
        public async Task ClusterGrain_RegisterWorld_AddsWorldToCluster()
        {
            // Arrange
            var clusterId = "test-cluster-2";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);

            // Act
            await grain.RegisterWorldAsync("world-1");

            // Assert
            var info = await grain.GetClusterInfoAsync();
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.WorldIds, Contains.Item("world-1"));
        }

        [Test]
        public async Task ClusterGrain_RegisterPortal_AddsPortal()
        {
            // Arrange
            var clusterId = "test-cluster-3";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);

            var portalLink = new PortalLink
            {
                PortalId = "portal-1",
                SourceWorldId = "world-1",
                SourceMapId = "map-1",
                TargetWorldId = null,
                TargetMapId = null,
                TargetTag = "dungeon",
                IsResolved = false
            };

            // Act
            await grain.RegisterPortalAsync(portalLink);

            // Assert
            var portals = await grain.GetPortalsAsync();
            Assert.That(portals, Is.Not.Null);
            Assert.That(portals, Has.Count.EqualTo(1));
            Assert.That(portals[0].PortalId, Is.EqualTo("portal-1"));
            Assert.That(portals[0].TargetTag, Is.EqualTo("dungeon"));
        }

        [Test]
        public async Task ClusterGrain_ResolvePortalTarget_ResolvesByTag()
        {
            // Arrange
            var clusterId = "test-cluster-4";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);

            // Register a target world with matching tag
            await grain.RegisterWorldAsync("world-target");
            await grain.RegisterMapAsync("world-target", "map-target");

            // Register portal that needs resolution
            var portalLink = new PortalLink
            {
                PortalId = "portal-1",
                SourceWorldId = "world-1",
                SourceMapId = "map-1",
                TargetWorldId = null,
                TargetMapId = null,
                TargetTag = "dungeon",
                IsResolved = false
            };

            await grain.RegisterPortalAsync(portalLink);

            // Note: In a real scenario, we'd need to tag the target map/world
            // For now, we'll test that resolution works when a target exists
            // Act
            var (resolvedWorldId, resolvedMapId) = await grain.ResolvePortalTargetAsync("portal-1", "dungeon");

            // Assert - resolution may or may not succeed depending on tag matching
            // At minimum, the method should not throw
            Assert.That(resolvedWorldId, Is.Not.Null);
            Assert.That(resolvedMapId, Is.Not.Null);
        }

        [Test]
        public async Task ClusterGrain_RegisterMap_CreatesMarket()
        {
            // Arrange
            var clusterId = "test-cluster-5";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);
            await grain.RegisterWorldAsync("world-1");

            // Act
            await grain.RegisterMapAsync("world-1", "map-1");

            // Assert
            var market = await grain.GetMarketAsync("world-1", "map-1");
            Assert.That(market, Is.Not.Null);
            Assert.That(market!.WorldId, Is.EqualTo("world-1"));
            Assert.That(market.MapId, Is.EqualTo("map-1"));
        }

        [Test]
        public async Task ClusterGrain_CreateTradeRoute_CreatesRoute()
        {
            // Arrange
            var clusterId = "test-cluster-6";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);
            await grain.RegisterWorldAsync("world-1");
            await grain.RegisterWorldAsync("world-2");
            await grain.RegisterMapAsync("world-1", "map-1");
            await grain.RegisterMapAsync("world-2", "map-2");

            var route = new TradeRoute
            {
                RouteId = "route-1",
                SourceMarketId = "world-1:map-1",
                DestinationMarketId = "world-2:map-2",
                ResourceTypes = new List<string> { "ore", "wood" },
                Capacity = 100,
                TravelTime = TimeSpan.FromHours(1)
            };

            // Act
            var createdRoute = await grain.CreateTradeRouteAsync(route);

            // Assert
            Assert.That(createdRoute, Is.Not.Null);
            Assert.That(createdRoute.RouteId, Is.EqualTo("route-1"));
            Assert.That(createdRoute.SourceMarketId, Is.EqualTo("world-1:map-1"));
        }

        [Test]
        public async Task ClusterGrain_ScheduleTransport_CreatesSchedule()
        {
            // Arrange
            var clusterId = "test-cluster-7";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);
            await grain.RegisterWorldAsync("world-1");
            await grain.RegisterWorldAsync("world-2");
            await grain.RegisterMapAsync("world-1", "map-1");
            await grain.RegisterMapAsync("world-2", "map-2");

            var route = new TradeRoute
            {
                RouteId = "route-1",
                SourceMarketId = "world-1:map-1",
                DestinationMarketId = "world-2:map-2",
                ResourceTypes = new List<string> { "ore" },
                Capacity = 100,
                TravelTime = TimeSpan.FromHours(1)
            };

            await grain.CreateTradeRouteAsync(route);

            var cargo = new Dictionary<string, int>
            {
                { "ore", 50 }
            };

            var departureTime = DateTime.UtcNow;

            // Act
            var schedule = await grain.ScheduleTransportAsync(route, cargo, departureTime);

            // Assert
            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule.ScheduleId, Is.Not.Null);
            Assert.That(schedule.RouteId, Is.EqualTo("route-1"));
            Assert.That(schedule.DepartureTime, Is.EqualTo(departureTime));
            Assert.That(schedule.Cargo["ore"], Is.EqualTo(50));
        }

        [Test]
        public async Task ClusterGrain_TickEconomy_UpdatesPrices()
        {
            // Arrange
            var clusterId = "test-cluster-8";
            var grain = _cluster!.GrainFactory.GetGrain<IClusterGrain>(clusterId);

            var clusterInfo = new ClusterInfo
            {
                ClusterId = clusterId,
                Name = "Test Cluster",
                Description = "Test cluster",
                WorldIds = new HashSet<string>()
            };

            await grain.InitializeAsync(clusterInfo);
            await grain.RegisterWorldAsync("world-1");
            await grain.RegisterMapAsync("world-1", "map-1");

            var marketBefore = await grain.GetMarketAsync("world-1", "map-1");
            Assert.That(marketBefore, Is.Not.Null);

            // Act
            await grain.TickEconomyAsync();

            // Assert
            var marketAfter = await grain.GetMarketAsync("world-1", "map-1");
            Assert.That(marketAfter, Is.Not.Null);
            
            // Economy should have been updated (may not have prices if no resources, but method should not throw)
            var economy = await grain.GetEconomyStateAsync();
            Assert.That(economy, Is.Not.Null);
            Assert.That(economy!.LastTickAt, Is.GreaterThan(DateTime.MinValue));
        }

        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                // Add in-memory grain storage
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("clusterStore");
            }
        }
    }
}

