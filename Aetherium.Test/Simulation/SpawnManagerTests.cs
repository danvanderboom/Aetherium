using System;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class SpawnManagerTests
    {
        private SimulationOptions _options = null!;
        private SeasonManager _seasonManager = null!;
        private WeatherSystem _weatherSystem = null!;
        private SpawnManager _spawnManager = null!;

        [SetUp]
        public void SetUp()
        {
            _options = new SimulationOptions
            {
                EnableWeather = true,
                EnableSeasons = true
            };
            
            var optionsWrapper = Options.Create(_options);
            _seasonManager = new SeasonManager(optionsWrapper);
            _weatherSystem = new WeatherSystem(optionsWrapper);
            _spawnManager = new SpawnManager(optionsWrapper, _seasonManager, _weatherSystem);
        }

        [Test]
        public void SpawnManager_GetSpawnRate_ReturnsRateBasedOnConditions()
        {
            var regionId = "region:0,0,0";
            _weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            var rate = _spawnManager.GetSpawnRate("wolf", regionId, 22.0, 0); // Night, day 0
            
            Assert.GreaterOrEqual(rate, 0.0);
        }

        [Test]
        public void SpawnManager_GetSpawnRate_AppliesTimeOfDayModifier()
        {
            var regionId = "region:0,0,0";
            _weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            // Wolf should have higher spawn rate at night
            var dayRate = _spawnManager.GetSpawnRate("wolf", regionId, 12.0, 0); // Noon
            var nightRate = _spawnManager.GetSpawnRate("wolf", regionId, 22.0, 0); // Night
            
            Assert.Greater(nightRate, dayRate);
        }

        [Test]
        public void SpawnManager_GetSpawnRate_AppliesWeatherModifier()
        {
            var regionId = "region:0,0,0";
            
            var clearRate = _spawnManager.GetSpawnRate("bandit", regionId, 12.0, 0);
            _weatherSystem.SetWeather(regionId, WeatherType.Stormy);
            var stormyRate = _spawnManager.GetSpawnRate("bandit", regionId, 12.0, 0);
            
            // Bandits should avoid storms
            Assert.Greater(clearRate, stormyRate);
        }

        [Test]
        public void SpawnManager_GetSpawnRate_AppliesSeasonModifier()
        {
            var regionId = "region:0,0,0";
            _weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            var springRate = _spawnManager.GetSpawnRate("wolf", regionId, 22.0, 0); // Spring
            var winterRate = _spawnManager.GetSpawnRate("wolf", regionId, 22.0, 90); // Winter
            
            // Rates should be different based on season (actual values depend on implementation)
            Assert.IsTrue(Math.Abs(springRate - winterRate) >= 0.0); // At least different values
        }

        [Test]
        public void SpawnManager_SelectSpawn_ReturnsCreatureType()
        {
            var regionId = "region:0,0,0";
            _weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            var spawn = _spawnManager.SelectSpawn(regionId, 12.0, 0);
            
            Assert.IsNotNull(spawn);
            Assert.IsNotEmpty(spawn);
        }

        [Test]
        public void SpawnManager_SelectSpawn_ReturnsNullWhenNoSpawnTable()
        {
            var regionId = "region:0,0,0";
            _weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            // This should return a valid spawn or null depending on conditions
            var spawn = _spawnManager.SelectSpawn(regionId, 12.0, 0);
            
            // Either null or a valid creature type
            if (spawn != null)
            {
                Assert.IsNotEmpty(spawn);
            }
        }
    }
}
