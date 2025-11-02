using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Aetherium.Server.Events;

namespace Aetherium.Test.Events
{
    [TestFixture]
    public class SpawnControllerGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
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
        public async Task SpawnEntitiesAsync_WithZeroSpawnRate_SpawnsNoEntities()
        {
            // Arrange
            var eventInstanceId = Guid.NewGuid().ToString();
            var spawnController = _cluster.GrainFactory.GetGrain<ISpawnControllerGrain>(eventInstanceId);

            var spawnConfig = new Dictionary<string, object>
            {
                { "spawnType", "Monster" },
                { "spawnRate", 0.0 }
            };

            // Act
            var result = await spawnController.SpawnEntitiesAsync(
                eventType: "monster_invasion",
                spawnConfig: spawnConfig,
                mapId: "map:dummy",
                x: 0, y: 0, z: 0,
                count: 3
            );

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.EntityIds, Is.Empty);
        }
    }
}
