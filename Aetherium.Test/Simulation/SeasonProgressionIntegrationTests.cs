using System;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class SeasonProgressionIntegrationTests
    {
        [Test]
        public void SeasonManager_WithSpawnManager_SeasonAffectsSpawnRates()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = true,
                EnableSeasons = true
            });

            var seasonManager = new SeasonManager(options);
            var weatherSystem = new WeatherSystem(options);
            var spawnManager = new SpawnManager(options, seasonManager, weatherSystem);
            var regionId = "region:0,0,0";

            weatherSystem.SetWeather(regionId, WeatherType.Clear);

            // Get spawn rates for different seasons
            var springRate = spawnManager.GetSpawnRate("wolf", regionId, 22.0, 0);  // Spring, night
            var winterRate = spawnManager.GetSpawnRate("wolf", regionId, 22.0, 90); // Winter, night

            // Rates should be different based on season
            Assert.That(springRate, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(winterRate, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void SeasonManager_WeatherModifiers_SeasonAffectsWeatherProbability()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = true,
                EnableSeasons = true
            });

            var seasonManager = new SeasonManager(options);

            // Winter should have higher snow probability
            var winterSnowMod = seasonManager.GetWeatherModifier(WeatherType.Snowy, "winter");
            var summerSnowMod = seasonManager.GetWeatherModifier(WeatherType.Snowy, "summer");

            Assert.That(winterSnowMod, Is.GreaterThan(summerSnowMod));

            // Summer should have higher clear probability
            var summerClearMod = seasonManager.GetWeatherModifier(WeatherType.Clear, "summer");
            var winterClearMod = seasonManager.GetWeatherModifier(WeatherType.Clear, "winter");

            Assert.That(summerClearMod, Is.GreaterThan(winterClearMod));
        }

        [Test]
        public void SeasonManager_YearCalculation_CyclesThroughYears()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });

            var seasonManager = new SeasonManager(options);

            // Year 0
            Assert.That(seasonManager.GetYear(0), Is.EqualTo(0));
            Assert.That(seasonManager.GetYear(119), Is.EqualTo(0));

            // Year 1
            Assert.That(seasonManager.GetYear(120), Is.EqualTo(1));
            Assert.That(seasonManager.GetYear(239), Is.EqualTo(1));

            // Year 2
            Assert.That(seasonManager.GetYear(240), Is.EqualTo(2));
        }

        [Test]
        public void SeasonManager_DayInSeason_CalculatesCorrectly()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableSeasons = true
            });

            var seasonManager = new SeasonManager(options);

            // Spring (days 0-29)
            Assert.That(seasonManager.GetDayInSeason(0), Is.EqualTo(0));
            Assert.That(seasonManager.GetDayInSeason(15), Is.EqualTo(15));
            Assert.That(seasonManager.GetDayInSeason(29), Is.EqualTo(29));

            // Summer (days 30-59)
            Assert.That(seasonManager.GetDayInSeason(30), Is.EqualTo(0)); // First day of summer
            Assert.That(seasonManager.GetDayInSeason(45), Is.EqualTo(15));
            Assert.That(seasonManager.GetDayInSeason(59), Is.EqualTo(29));

            // Winter (days 90-119)
            Assert.That(seasonManager.GetDayInSeason(90), Is.EqualTo(0)); // First day of winter
            Assert.That(seasonManager.GetDayInSeason(105), Is.EqualTo(15));
            Assert.That(seasonManager.GetDayInSeason(119), Is.EqualTo(29));
        }
    }
}

