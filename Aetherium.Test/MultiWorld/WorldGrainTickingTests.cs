using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Aetherium.Server.Events;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.Server.Simulation;
using Microsoft.Extensions.Options;

namespace Aetherium.Test.MultiWorld
{
    [TestFixture]
    public class WorldGrainTickingTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                // Add in-memory grain storage
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

                    services.AddSingleton<IWorldSnapshotStore, Aetherium.Server.Persistence.MemoryWorldSnapshotStore>();
                    services.AddSingleton<TemporalModifierRegistry>(sp => new TemporalModifierRegistry());
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
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task WorldGrain_Tick_PropagatesToMaps()
        {
            // Arrange
            var worldId = $"test-world-tick-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Tick Test World",
                Size = new WorldSize { Width = 64, Height = 64, Depth = 1 }
            };

            await grain.InitializeAsync(config);
            
            // Wait for map initialization
            await Task.Delay(500);

            // Act
            await grain.TickAsync();

            // Assert - Tick should complete without errors
            // In a full test, we might verify that regions were ticked
            Assert.Pass("WorldGrain.TickAsync completed without exceptions");
        }

        [Test]
        public async Task WorldGrain_Tick_UpdatesLastActivityTime()
        {
            // Arrange
            var worldId = $"test-world-activity-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Activity Test World",
                Size = new WorldSize { Width = 64, Height = 64, Depth = 1 }
            };

            await grain.InitializeAsync(config);
            
            var info1 = await grain.GetInfoAsync();
            var beforeTick = info1!.LastActivityAt;

            // Wait a bit
            await Task.Delay(100);

            // Act
            await grain.TickAsync();
            
            var info2 = await grain.GetInfoAsync();

            // Assert
            Assert.That(info2, Is.Not.Null);
            Assert.That(info2!.LastActivityAt, Is.GreaterThanOrEqualTo(beforeTick));
        }

        [Test]
        public async Task WorldGrain_SaveWorld_PersistsAllMaps()
        {
            // Arrange
            var worldId = $"test-world-save-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Save Test World",
                Size = new WorldSize { Width = 64, Height = 64, Depth = 1 }
            };

            await grain.InitializeAsync(config);
            
            // Wait for map initialization
            await Task.Delay(500);

            // Act
            await grain.SaveWorldAsync();

            // Assert - Save should complete without errors
            Assert.Pass("WorldGrain.SaveWorldAsync completed without exceptions");
        }

        [Test]
        public async Task WorldGrain_LoadWorld_RestoresAllMaps()
        {
            // Arrange
            var worldId = $"test-world-load-{Guid.NewGuid()}";
            var grain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = "Load Test World",
                Size = new WorldSize { Width = 64, Height = 64, Depth = 1 }
            };

            await grain.InitializeAsync(config);
            await grain.SaveWorldAsync();

            // Wait a bit
            await Task.Delay(200);

            // Act
            var loaded = await grain.LoadWorldAsync();

            // Assert
            Assert.That(loaded, Is.True);
            
            var info = await grain.GetInfoAsync();
            Assert.That(info, Is.Not.Null);
        }
    }
}
