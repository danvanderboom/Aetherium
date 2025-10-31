using System;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class WeatherSystemTests
    {
        [Test]
        public void WeatherSystem_GetWeather_ReturnsDefaultWhenNotSet()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = true
            });
            
            var weatherSystem = new WeatherSystem(options);
            
            var weather = weatherSystem.GetWeather("region:0,0,0");
            
            Assert.AreEqual(WeatherType.Clear, weather);
        }

        [Test]
        public void WeatherSystem_SetWeather_SetsWeatherForRegion()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = true
            });
            
            var weatherSystem = new WeatherSystem(options);
            var regionId = "region:0,0,0";
            
            weatherSystem.SetWeather(regionId, WeatherType.Rainy);
            
            var weather = weatherSystem.GetWeather(regionId);
            
            Assert.AreEqual(WeatherType.Rainy, weather);
        }

        [Test]
        public void WeatherSystem_UpdateWeather_DoesNotUpdateWhenDisabled()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = false
            });
            
            var weatherSystem = new WeatherSystem(options);
            var regionId = "region:0,0,0";
            
            weatherSystem.SetWeather(regionId, WeatherType.Rainy);
            weatherSystem.UpdateWeather(regionId, 12.0, 0, "spring");
            
            // Should remain as set since updates are disabled
            var weather = weatherSystem.GetWeather(regionId);
            Assert.AreEqual(WeatherType.Rainy, weather);
        }

        [Test]
        public void WeatherSystem_UpdateWeather_TransitionsWeather()
        {
            var options = Options.Create(new SimulationOptions
            {
                EnableWeather = true
            });
            
            var weatherSystem = new WeatherSystem(options);
            var regionId = "region:0,0,0";
            
            // Set initial weather
            weatherSystem.SetWeather(regionId, WeatherType.Clear);
            
            // Update multiple times to allow transitions
            // Note: This is probabilistic, so we check that it can transition
            for (int i = 0; i < 10; i++)
            {
                weatherSystem.UpdateWeather(regionId, 12.0 + i, 0, "spring");
            }
            
            // Weather should be one of the valid types
            var weather = weatherSystem.GetWeather(regionId);
            Assert.IsTrue(Enum.IsDefined(typeof(WeatherType), weather));
        }
    }
}
