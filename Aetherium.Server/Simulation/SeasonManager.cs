using System;
using Microsoft.Extensions.Options;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Manages seasonal progression based on game time.
    /// Seasons affect weather patterns, spawn rates, and biome appearance.
    /// </summary>
    public class SeasonManager
    {
        private readonly SimulationOptions _options;
        private const int DaysPerSeason = 30; // 4 seasons per year = 120 days per year

        public SeasonManager(IOptions<SimulationOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// Gets the current season based on game day.
        /// </summary>
        public string GetSeason(int day)
        {
            if (!_options.EnableSeasons)
                return "spring"; // Default season

            // Calculate season from day number (0-based)
            var dayOfYear = day % (DaysPerSeason * 4); // Modulo to cycle seasons
            var seasonIndex = dayOfYear / DaysPerSeason;

            return seasonIndex switch
            {
                0 => "spring",
                1 => "summer",
                2 => "fall",
                3 => "winter",
                _ => "spring"
            };
        }

        /// <summary>
        /// Gets the day within the current season (0-29).
        /// </summary>
        public int GetDayInSeason(int day)
        {
            var dayOfYear = day % (DaysPerSeason * 4);
            return dayOfYear % DaysPerSeason;
        }

        /// <summary>
        /// Gets the year number (starts at 0).
        /// </summary>
        public int GetYear(int day)
        {
            return day / (DaysPerSeason * 4);
        }

        /// <summary>
        /// Gets spawn rate modifier for a creature type based on season.
        /// </summary>
        public double GetSpawnModifier(string creatureType, string season)
        {
            // Seasonal spawn modifiers
            // Example: some creatures are more common in certain seasons
            var modifiers = creatureType.ToLowerInvariant() switch
            {
                "wolf" => season switch
                {
                    "winter" => 1.5, // More wolves in winter
                    "summer" => 0.7,  // Fewer in summer
                    _ => 1.0
                },
                "bandit" => season switch
                {
                    "winter" => 0.6,  // Fewer bandits in harsh winter
                    "summer" => 1.3,  // More in summer (travel season)
                    _ => 1.0
                },
                "bear" => season switch
                {
                    "winter" => 0.2,  // Bears hibernate in winter
                    "spring" => 1.2, // More active in spring
                    _ => 1.0
                },
                _ => 1.0
            };

            return modifiers;
        }

        /// <summary>
        /// Gets weather probability modifier for a weather type based on season.
        /// </summary>
        public double GetWeatherModifier(WeatherType weather, string season)
        {
            return (weather, season) switch
            {
                (WeatherType.Snowy, "winter") => 3.0,  // Much more snow in winter
                (WeatherType.Snowy, "summer") => 0.0, // No snow in summer
                (WeatherType.Rainy, "spring") => 1.5,  // More rain in spring
                (WeatherType.Rainy, "fall") => 1.3,    // More rain in fall
                (WeatherType.Rainy, "summer") => 0.8,  // Less rain in summer
                (WeatherType.Clear, "summer") => 1.4,  // More clear days in summer
                (WeatherType.Clear, "winter") => 0.7,  // Fewer clear days in winter
                _ => 1.0
            };
        }
    }
}

