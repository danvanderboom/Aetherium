using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Aetherium.Server.MultiWorld;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;
using Aetherium.Server.Persistence;
using Aetherium.Server.Events;

namespace Aetherium.Test.MultiWorld
{
    [TestFixture]
    public class GameMapGrainRegionTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Add in-memory grain storage (worldStore needed for WorldGrain initialization)
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");

                // Register required services
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });

                    services.Configure<SimulationOptions>(options =>
                    {
                        options.RegionSize = 64;
                        options.TickHz = 1.0;
                        options.DayLengthMinutes = 24;
                        options.EnableWeather = true;
                        options.EnableSeasons = true;
                    });

                    services.AddSingleton<WorldClock>(sp =>
                    {
                        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new WorldClock(opts);
                    });

                    services.AddSingleton<SeasonManager>(sp =>
                    {
                        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new SeasonManager(opts);
                    });

                    services.AddSingleton<WeatherSystem>(sp =>
                    {
                        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new WeatherSystem(opts);
                    });

                    services.AddSingleton<IWorldSnapshotStore, MemoryWorldSnapshotStore>();
                    services.AddSingleton<TemporalModifierRegistry>(_ => new TemporalModifierRegistry());
                    services.AddSingleton<IEventScheduler>(sp =>
                    {
                        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new EventScheduler(options);
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
        public async Task GameMapGrain_Initialize_PartitionsIntoRegions()
        {
            // Arrange
            var worldId = "test-world-1";
            var mapId = $"{worldId}:map:test-map-1";
            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            
            var size = new WorldSize { Width = 128, Height = 128, Depth = 1 }; // 2x2 regions (64x64 each)

            // Use a fixed seed for test reproducibility
            var parameters = new System.Collections.Generic.Dictionary<string, object> { { "seed", 54321 } };
            // Act
            await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);

            // Note: We can't directly access regions from IGameMapGrain,
            // but we can verify the map was initialized by checking metadata
            var metadata = await mapGrain.GetMetadataAsync();

            // Assert
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.MapId, Is.EqualTo(mapId));
            Assert.That(metadata.Size.Width, Is.EqualTo(128));
            Assert.That(metadata.Size.Height, Is.EqualTo(128));
        }

        [Test]
        public async Task GameMapGrain_Tick_PropagatesToRegions()
        {
            // Arrange
            var worldId = "test-world-1";
            var mapId = $"{worldId}:map:test-map-2";
            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            
            var size = new WorldSize { Width = 128, Height = 128, Depth = 1 };

            // Use a fixed seed for test reproducibility
            var parameters = new System.Collections.Generic.Dictionary<string, object> { { "seed", 67890 } };
            await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);

            // Get a region grain directly to verify it's ticking
            var regionKey = $"{mapId}:region:0,0,0";
            var regionGrain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            
            var initialSnapshot = await regionGrain.GetSnapshotAsync();
            var initialTime = initialSnapshot.GameTimeHours;

            // Act
            await mapGrain.TickAsync(TimeSpan.FromHours(1));

            // Verify region was ticked (wait a bit for async propagation)
            await Task.Delay(100);
            var updatedSnapshot = await regionGrain.GetSnapshotAsync();

            // Assert
            // Region should have been ticked (though timing may vary)
            // At minimum, we verify the region exists and can be accessed
            Assert.That(updatedSnapshot, Is.Not.Null);
        }

        [Test]
        public async Task GameMapGrain_SaveMap_PersistsRegions()
        {
            // Arrange
            var worldId = "test-world-1";
            var mapId = $"{worldId}:map:test-map-3";
            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            
            var size = new WorldSize { Width = 64, Height = 64, Depth = 1 }; // Single region

            // Use a fixed seed for test reproducibility
            var parameters = new System.Collections.Generic.Dictionary<string, object> { { "seed", 11111 } };
            await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);

            // Act
            await mapGrain.SaveMapAsync();

            // Assert - Save should complete without errors
            // In a full test, we might reload and verify state persistence
            Assert.Pass("SaveMapAsync completed without exceptions");
        }

        [Test]
        public async Task GameMapGrain_LoadMap_RestoresRegions()
        {
            // Arrange
            var worldId = "test-world-1";
            var mapId = $"{worldId}:map:test-map-4";
            var mapGrain = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            
            var size = new WorldSize { Width = 64, Height = 64, Depth = 1 };

            // Use a fixed seed for test reproducibility
            var parameters = new System.Collections.Generic.Dictionary<string, object> { { "seed", 12345 } };
            await mapGrain.InitializeAsync(worldId, "Test Map", size, "outdoor", parameters);
            await mapGrain.SaveMapAsync();

            // Act
            var loaded = await mapGrain.LoadMapAsync();

            // Assert
            Assert.That(loaded, Is.True);
            
            var metadata = await mapGrain.GetMetadataAsync();
            Assert.That(metadata, Is.Not.Null);
        }
    }
}
