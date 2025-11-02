using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Events;
using Aetherium.Model.Events;
using Aetherium.Model.Worlds;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Aetherium.Test.Events
{
    [TestFixture]
    public class EventInstanceGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IWorldSnapshotStore, MemoryWorldSnapshotStore>();
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task Initialize_ResolvesMapId_FromRegionId_WhenMapIdMissing()
        {
            // Arrange: initialize a region with a known map id
            var mapId = "map:test-resolution";
            var regionKey = $"{mapId}:region:0,0,0";
            var region = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            await region.InitializeAsync(mapId, 0, 0, 0, 64);

            // Create an event instance with only RegionId
            var eventInstanceId = new EventInstanceId(Guid.NewGuid().ToString());
            var config = new EventInstanceConfig
            {
                EventInstanceId = eventInstanceId,
                EventId = Guid.NewGuid().ToString(),
                EventType = "test_event",
                WorldId = new WorldId("world:test"),
                MapId = null, // missing on purpose
                RegionId = regionKey,
                X = 1,
                Y = 2,
                Z = 0,
                AreaOfInterestRadius = 10,
                ScheduledGameTime = 0.0
            };

            var instance = _cluster.GrainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId.Value);

            // Act
            await instance.InitializeAsync(config);
            var info = await instance.GetInfoAsync();

            // Assert
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.MapId, Is.EqualTo(mapId));
        }

        [Test]
        public async Task CompleteAsync_SetsCompletedState_AndBroadcasts()
        {
            // Arrange
            var worldId = new WorldId("world-complete");
            var eventInstanceId = new EventInstanceId("event-complete");
            var regionId = "mapX:region:0,0,0";
            var mapId = "mapX";

            // Seed region map resolution
            var mockRegion = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionId);
            await mockRegion.LoadSnapshotAsync(new RegionStateSnapshot
            {
                RegionId = regionId,
                MapId = mapId,
                RegionX = 0, RegionY = 0, ZLevel = 0, RegionSize = 64,
                SavedAt = DateTime.UtcNow,
                GameTimeHours = 0.0
            });

            var config = new EventInstanceConfig
            {
                EventInstanceId = eventInstanceId,
                EventId = "evt-1",
                EventType = "merchant_caravan",
                WorldId = worldId,
                RegionId = regionId,
                X = 5, Y = 6, Z = 0,
                AreaOfInterestRadius = 50,
                EventData = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                ScheduledGameTime = 10.0
            };

            var grain = _cluster.GrainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId.Value);
            await grain.InitializeAsync(config);

            // Act
            await grain.CompleteAsync();
            var info = await grain.GetInfoAsync();

            // Assert: state updated
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.State, Is.EqualTo(EventInstanceState.Completed));
            Assert.That(info.CompletedAt, Is.Not.Null);
        }
    }
}
