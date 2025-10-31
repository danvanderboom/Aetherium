using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Manages creature spawning with time-of-day and weather-weighted spawn tables.
    /// </summary>
    public class SpawnManager
    {
        private readonly SimulationOptions _options;
        private readonly SeasonManager _seasonManager;
        private readonly WeatherSystem _weatherSystem;

        public SpawnManager(
            IOptions<SimulationOptions> options,
            SeasonManager seasonManager,
            WeatherSystem weatherSystem)
        {
            _options = options.Value;
            _seasonManager = seasonManager;
            _weatherSystem = weatherSystem;
        }

        /// <summary>
        /// Gets spawn rate for a creature type at a given time and weather.
        /// </summary>
        public double GetSpawnRate(string creatureType, string regionId, double timeOfDay, int day)
        {
            // Base spawn rate (from spawn table)
            var baseRate = GetBaseSpawnRate(creatureType);

            // Time-of-day modifier
            var timeModifier = GetTimeOfDayModifier(creatureType, timeOfDay);

            // Weather modifier
            var weather = _weatherSystem.GetWeather(regionId);
            var weatherModifier = GetWeatherModifier(creatureType, weather);

            // Season modifier
            var season = _seasonManager.GetSeason(day);
            var seasonModifier = _seasonManager.GetSpawnModifier(creatureType, season);

            return baseRate * timeModifier * weatherModifier * seasonModifier;
        }

        /// <summary>
        /// Selects a creature to spawn based on weighted spawn table.
        /// </summary>
        public string? SelectSpawn(string regionId, double timeOfDay, int day)
        {
            var spawnTable = GetSpawnTableForConditions(regionId, timeOfDay, day);
            if (spawnTable.Count == 0)
                return null;

            var totalWeight = spawnTable.Sum(s => s.Weight);
            var roll = new Random().NextDouble() * totalWeight;

            var cumulative = 0.0;
            foreach (var entry in spawnTable)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    return entry.CreatureType;
                }
            }

            return spawnTable[0].CreatureType; // Fallback
        }

        private List<(string CreatureType, double Weight)> GetSpawnTableForConditions(
            string regionId, double timeOfDay, int day)
        {
            var season = _seasonManager.GetSeason(day);
            var weather = _weatherSystem.GetWeather(regionId);

            // Base spawn table (example - in real implementation would come from config/data)
            var baseTable = new List<(string, double)>
            {
                ("Wolf", 0.2),
                ("Bandit", 0.15),
                ("Bear", 0.05),
                ("Monster", 0.3),
                ("Snake", 0.1),
                ("Zombie", 0.2)
            };

            // Apply modifiers to create weighted table
            var weightedTable = baseTable.Select(entry =>
            {
                var spawnRate = GetSpawnRate(entry.Item1, regionId, timeOfDay, day);
                return (entry.Item1, entry.Item2 * spawnRate);
            }).Where(e => e.Item2 > 0.0).ToList();

            return weightedTable;
        }

        private double GetBaseSpawnRate(string creatureType)
        {
            // Base spawn rates (from config/data)
            return creatureType.ToLowerInvariant() switch
            {
                "wolf" => 0.2,
                "bandit" => 0.15,
                "bear" => 0.05,
                "monster" => 0.3,
                "snake" => 0.1,
                "zombie" => 0.2,
                _ => 0.1
            };
        }

        private double GetTimeOfDayModifier(string creatureType, double timeOfDay)
        {
            // Time-of-day spawn modifiers
            return creatureType.ToLowerInvariant() switch
            {
                "wolf" => timeOfDay >= 18.0 || timeOfDay <= 6.0 ? 2.0 : 0.5, // More active at night
                "zombie" => timeOfDay >= 20.0 || timeOfDay <= 6.0 ? 1.5 : 0.8, // More active at night
                "bandit" => timeOfDay >= 8.0 && timeOfDay <= 20.0 ? 1.3 : 0.7, // More active during day
                "bear" => timeOfDay >= 6.0 && timeOfDay <= 18.0 ? 1.2 : 0.6, // Active during day
                _ => 1.0
            };
        }

        private double GetWeatherModifier(string creatureType, WeatherType weather)
        {
            // Weather spawn modifiers
            return creatureType.ToLowerInvariant() switch
            {
                "wolf" => weather switch
                {
                    WeatherType.Rainy => 0.7,  // Less active in rain
                    WeatherType.Snowy => 1.3,   // More active in snow
                    _ => 1.0
                },
                "bandit" => weather switch
                {
                    WeatherType.Stormy => 0.3, // Avoid storms
                    WeatherType.Rainy => 0.6,   // Less active in rain
                    _ => 1.0
                },
                _ => 1.0
            };
        }
    }
}

