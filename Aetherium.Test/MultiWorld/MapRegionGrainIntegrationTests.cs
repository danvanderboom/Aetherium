using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Aetherium.Server.Events;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.Server.Simulation;
using Microsoft.Extensions.Options;

namespace Aetherium.Test.MultiWorld
{
    [TestFixture]
    public class MapRegionGrainIntegrationTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            private static TestCluster? _cluster;
            
            public static void SetCluster(TestCluster cluster) => _cluster = cluster;

            public void Configure(ISiloBuilder siloBuilder)
            {
                // Add in-memory grain storage (worldStore needed for WorldGrain/ClusterGrain if accessed)
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");

                // Register simulation services
                siloBuilder.ConfigureServices(services =>
                {
                    services.Configure<SimulationOptions>(options =>
                    {
                        options.TickHz = 1.0;
                        options.DayLengthMinutes = 24;
                        options.RegionSize = 64;
                        options.EnableWeather = true;
                        options.EnableSeasons = true;
                    });

                    services.AddSingleton<WorldClock>(sp =>
                    {
                        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new WorldClock(options);
                    });

                    services.AddSingleton<SeasonManager>(sp =>
                    {
                        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new SeasonManager(options);
                    });

                    services.AddSingleton<WeatherSystem>(sp =>
                    {
                        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new WeatherSystem(options);
                    });

                    services.AddSingleton<IWorldSnapshotStore, MemoryWorldSnapshotStore>();

                    services.AddSingleton<TemporalModifierRegistry>(sp =>
                    {
                        var registry = new TemporalModifierRegistry();
                        // Don't register actual modifiers for basic tests
                        return registry;
                    });

                    services.AddSingleton<IEventScheduler>(sp =>
                    {
                        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                        return new Aetherium.Server.Events.EventScheduler(options);
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
        public async Task MapRegionGrain_Initialize_SetsRegionBounds()
        {
            // Arrange
            var mapId = "test-map-1";
            var regionKey = $"{mapId}:region:0,0,0";
            var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);

            // Act
            await grain.InitializeAsync(mapId, 0, 0, 0, 64);
            var snapshot = await grain.GetSnapshotAsync();

            // Assert
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.MapId, Is.EqualTo(mapId));
            Assert.That(snapshot.RegionX, Is.EqualTo(0));
            Assert.That(snapshot.RegionY, Is.EqualTo(0));
            Assert.That(snapshot.ZLevel, Is.EqualTo(0));
            Assert.That(snapshot.RegionSize, Is.EqualTo(64));
        }

        [Test]
        public async Task MapRegionGrain_Tick_UpdatesGameTime()
        {
            // Arrange
            var mapId = "test-map-1";
            var regionKey = $"{mapId}:region:0,0,0";
            var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            await grain.InitializeAsync(mapId, 0, 0, 0, 64);

            var initialSnapshot = await grain.GetSnapshotAsync();
            var initialTime = initialSnapshot.GameTimeHours;

            // Act
            await grain.TickAsync(TimeSpan.FromHours(1));
            var updatedSnapshot = await grain.GetSnapshotAsync();

            // Assert
            Assert.That(updatedSnapshot.GameTimeHours, Is.GreaterThan(initialTime));
            Assert.That(updatedSnapshot.GameTimeHours, Is.EqualTo(initialTime + 1.0).Within(0.1));
        }

        [Test]
        public async Task MapRegionGrain_RecordTraversal_UpdatesHeatmap()
        {
            // Arrange
            var mapId = "test-map-1";
            var regionKey = $"{mapId}:region:0,0,0";
            var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            await grain.InitializeAsync(mapId, 0, 0, 0, 64);

            // Act
            await grain.RecordTraversalAsync(10, 20);
            await grain.RecordTraversalAsync(10, 20); // Same location twice
            await grain.RecordTraversalAsync(15, 25); // Different location

            var heatmap = await grain.GetTraversalHeatmapAsync();

            // Assert
            Assert.That(heatmap, Is.Not.Null);
            Assert.That(heatmap.ContainsKey((10, 20)), Is.True);
            Assert.That(heatmap[(10, 20)], Is.EqualTo(2)); // Visited twice
            Assert.That(heatmap.ContainsKey((15, 25)), Is.True);
            Assert.That(heatmap[(15, 25)], Is.EqualTo(1)); // Visited once
        }

        [Test]
        public async Task MapRegionGrain_ApplyDelta_UpdatesTerrain()
        {
            // Arrange
            var mapId = "test-map-1";
            var regionKey = $"{mapId}:region:0,0,0";
            var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            await grain.InitializeAsync(mapId, 0, 0, 0, 64);

            var delta = new RegionDelta
            {
                RegionId = regionKey,
                Timestamp = DateTime.UtcNow,
                Type = DeltaType.TerrainModified,
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["location"] = "10,20",
                    ["terrainType"] = "stone"
                }
            };

            // Act
            await grain.ApplyDeltaAsync(delta);
            var snapshot = await grain.GetSnapshotAsync();

            // Assert
            Assert.That(snapshot.TerrainModifications, Is.Not.Null);
            Assert.That(snapshot.TerrainModifications.ContainsKey("10,20"), Is.True);
            Assert.That(snapshot.TerrainModifications["10,20"], Is.EqualTo("stone"));
        }

        [Test]
        public async Task MapRegionGrain_LoadSnapshot_RestoresState()
        {
            // Arrange
            var mapId = "test-map-1";
            var regionKey = $"{mapId}:region:0,0,0";
            var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);
            
            var originalSnapshot = new RegionStateSnapshot
            {
                RegionId = regionKey,
                MapId = mapId,
                RegionX = 0,
                RegionY = 0,
                ZLevel = 0,
                RegionSize = 64,
                GameTimeHours = 10.5,
                TerrainModifications = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["5,5"] = "grass"
                }
            };

            // Act
            await grain.LoadSnapshotAsync(originalSnapshot);
            var loadedSnapshot = await grain.GetSnapshotAsync();

            // Assert
            Assert.That(loadedSnapshot.RegionId, Is.EqualTo(originalSnapshot.RegionId));
            Assert.That(loadedSnapshot.GameTimeHours, Is.EqualTo(originalSnapshot.GameTimeHours));
            Assert.That(loadedSnapshot.TerrainModifications.ContainsKey("5,5"), Is.True);
            Assert.That(loadedSnapshot.TerrainModifications["5,5"], Is.EqualTo("grass"));
        }
    }
}
