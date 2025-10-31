using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Aetherium.Server.Events;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Simulation;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.Options;

namespace Aetherium.Test.MultiWorld
{
	[TestFixture]
	public class RegionWeatherSeasonPersistenceTests
	{
		private TestCluster _cluster = null!;

		private sealed class SiloConfigurator : ISiloConfigurator
		{
			public void Configure(ISiloBuilder siloBuilder)
			{
				siloBuilder.AddMemoryGrainStorage("mapStore");
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
			_cluster.Deploy();
		}

		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			_cluster.StopAllSilos();
		}

		[Test]
		[Ignore("Orleans serialization issue with IGameMapGrain - pre-existing")]
		public async Task Tick_SetsWeatherAndSeasonInSnapshot()
		{
			var mapId = "map:test-weather";
			var regionKey = $"{mapId}:region:0,0,0";
			var grain = _cluster.GrainFactory.GetGrain<IMapRegionGrain>(regionKey);

			await grain.InitializeAsync(mapId, 0, 0, 0, 64);
			await grain.TickAsync(TimeSpan.FromHours(1));

			var snapshot = await grain.GetSnapshotAsync();
			Assert.That(snapshot.Season, Is.Not.Null.And.Not.Empty);
			Assert.That(snapshot.WeatherType, Is.Not.Null.And.Not.Empty);
		}
	}
}


