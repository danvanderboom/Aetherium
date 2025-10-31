using System;
using System.Collections.Generic;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Model;
using Aetherium.Server;
using Aetherium.Server.Simulation;
using World = Aetherium.Core.World;
using TileType = Aetherium.Core.TileType;
using TerrainType = Aetherium.Core.TerrainType;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
	[TestFixture]
	public class PerceptionTimeWeatherTests
	{
		private World _world = null!;
		private WorldLocation _playerLocation = null!;
		private WorldClock _clock = null!;
		private WeatherSystem _weather = null!;
		private SeasonManager _seasons = null!;
		private PerceptionService _perception = null!;

		[SetUp]
		public void SetUp()
		{
			_world = new World();
			_playerLocation = new WorldLocation(0, 0, 0);

			var tileTypes = new List<TileType>
			{
				new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
				new TileType { Name = "Wall", Settings = new Dictionary<string, string>() }
			};
			_world.AddTileTypes(tileTypes);
			var terrainTypes = new List<TerrainType>
			{
				new TerrainType { Name = "Plains", TileType = tileTypes[0] },
				new TerrainType { Name = "Wall", TileType = tileTypes[1] }
			};
			_world.AddTerrainTypes(terrainTypes);
			_world.SetTerrain("Plains", _playerLocation);

			var options = Options.Create(new SimulationOptions
			{
				DayLengthMinutes = 24,
				EnableWeather = true,
				EnableSeasons = true
			});
			_clock = new WorldClock(options);
			_weather = new WeatherSystem(options);
			_seasons = new SeasonManager(options);
			_perception = new PerceptionService(_clock, _weather, _seasons);
		}

		[Test]
		public void Perception_IncludesTimeOfDayAndWeather()
		{
			_clock.SetWorldTime(12.0); // Noon
			var regionId = "region:0,0,0"; // Derived from (0,0,0)
			_weather.SetWeather(regionId, WeatherType.Rainy);

			var dto = _perception.ComputePerception(
				_world,
				_playerLocation,
				Aetherium.WorldDirection.North,
				new System.Drawing.Size(20, 20),
				LightingMode.Sunlight,
				VisionMode.Normal,
				null,
				DateTime.UtcNow);

			Assert.That(dto.GameTimeOfDay, Is.EqualTo(12.0).Within(0.2));
			Assert.That(dto.Weather, Is.EqualTo("Rainy"));
		}

		[Test]
		public void Perception_IncludesSeasonFromClock()
		{
			// Day 95 -> Winter
			_clock.SetWorldTime(95 * 24.0);

			var dto = _perception.ComputePerception(
				_world,
				_playerLocation,
				Aetherium.WorldDirection.North,
				new System.Drawing.Size(20, 20),
				LightingMode.Sunlight,
				VisionMode.Normal,
				null,
				DateTime.UtcNow);

			Assert.That(dto.Season, Is.EqualTo("winter"));
		}
	}
}


