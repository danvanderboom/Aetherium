using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Manages weather patterns that change over time.
    /// Weather affects spawns, visibility, and other game mechanics.
    /// </summary>
    public class WeatherSystem
    {
        private readonly SimulationOptions _options;
        private readonly Dictionary<string, WeatherState> _regionWeather = new();
        private readonly Random _random = new();

        public WeatherSystem(IOptions<SimulationOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// Gets the current weather for a region.
        /// </summary>
        public WeatherType GetWeather(string regionId)
        {
            if (_regionWeather.TryGetValue(regionId, out var state))
            {
                return state.CurrentWeather;
            }
            
            // Default weather
            return WeatherType.Clear;
        }

        /// <summary>
        /// Updates weather for a region based on time progression.
        /// </summary>
        public void UpdateWeather(string regionId, double timeOfDay, int day, string? season = null)
        {
            if (!_options.EnableWeather)
                return;

            if (!_regionWeather.TryGetValue(regionId, out var state))
            {
                state = new WeatherState
                {
                    RegionId = regionId,
                    CurrentWeather = WeatherType.Clear,
                    LastChangeTime = timeOfDay,
                    Season = season ?? "spring"
                };
                _regionWeather[regionId] = state;
            }

            // Weather changes can occur hourly or based on season
            var hoursSinceChange = timeOfDay - state.LastChangeTime;
            if (hoursSinceChange < 0) hoursSinceChange += 24.0; // Handle day wrap

            // Determine weather transition probability based on season and current weather
            var transitionChance = GetWeatherTransitionChance(state, season, timeOfDay);
            
            if (_random.NextDouble() < transitionChance)
            {
                state.CurrentWeather = GetNextWeather(state.CurrentWeather, season, timeOfDay);
                state.LastChangeTime = timeOfDay;
            }
        }

        private double GetWeatherTransitionChance(WeatherState state, string? season, double timeOfDay)
        {
            // Base transition chance: 10% per hour
            var baseChance = 0.1;

            // Season modifiers
            var seasonModifier = season?.ToLowerInvariant() switch
            {
                "winter" => 1.5, // More weather changes in winter
                "spring" => 1.2, // Moderate changes in spring
                "summer" => 0.8, // Fewer changes in summer
                "fall" => 1.3,   // More changes in fall
                _ => 1.0
            };

            // Time-of-day modifiers (more likely to change at transition times)
            var timeModifier = (timeOfDay >= 6.0 && timeOfDay <= 8.0) || (timeOfDay >= 18.0 && timeOfDay <= 20.0)
                ? 1.5  // Higher chance at dawn/dusk
                : 1.0;

            return baseChance * seasonModifier * timeModifier;
        }

        private WeatherType GetNextWeather(WeatherType current, string? season, double timeOfDay)
        {
            // Weather transition probabilities based on current weather and season
            var transitions = GetWeatherTransitions(current, season);
            
            // Random selection based on weights
            var totalWeight = transitions.Sum(t => t.Weight);
            var roll = _random.NextDouble() * totalWeight;
            
            var cumulative = 0.0;
            foreach (var transition in transitions)
            {
                cumulative += transition.Weight;
                if (roll <= cumulative)
                {
                    return transition.WeatherType;
                }
            }

            return current; // Fallback
        }

        private List<(WeatherType WeatherType, double Weight)> GetWeatherTransitions(WeatherType current, string? season)
        {
            var transitions = new List<(WeatherType, double)>();

            switch (current)
            {
                case WeatherType.Clear:
                    transitions.Add((WeatherType.Clear, 0.5));
                    transitions.Add((WeatherType.Cloudy, 0.3));
                    transitions.Add((WeatherType.Rainy, season == "spring" || season == "fall" ? 0.15 : 0.05));
                    transitions.Add((WeatherType.Snowy, season == "winter" ? 0.05 : 0.0));
                    break;
                case WeatherType.Cloudy:
                    transitions.Add((WeatherType.Clear, 0.4));
                    transitions.Add((WeatherType.Cloudy, 0.3));
                    transitions.Add((WeatherType.Rainy, season == "spring" || season == "fall" ? 0.25 : 0.1));
                    transitions.Add((WeatherType.Snowy, season == "winter" ? 0.05 : 0.0));
                    break;
                case WeatherType.Rainy:
                    transitions.Add((WeatherType.Clear, 0.2));
                    transitions.Add((WeatherType.Cloudy, 0.5));
                    transitions.Add((WeatherType.Rainy, 0.3));
                    break;
                case WeatherType.Snowy:
                    transitions.Add((WeatherType.Clear, 0.1));
                    transitions.Add((WeatherType.Cloudy, 0.3));
                    transitions.Add((WeatherType.Snowy, 0.6));
                    break;
            }

            return transitions;
        }

        /// <summary>
        /// Sets weather for a region (for testing or special events).
        /// </summary>
        public void SetWeather(string regionId, WeatherType weather)
        {
            if (_regionWeather.TryGetValue(regionId, out var state))
            {
                state.CurrentWeather = weather;
            }
            else
            {
                _regionWeather[regionId] = new WeatherState
                {
                    RegionId = regionId,
                    CurrentWeather = weather,
                    LastChangeTime = 0.0
                };
            }
        }
    }

    public enum WeatherType
    {
        Clear,
        Cloudy,
        Rainy,
        Snowy,
        Foggy,
        Stormy
    }

    internal class WeatherState
    {
        public string RegionId { get; set; } = string.Empty;
        public WeatherType CurrentWeather { get; set; }
        public double LastChangeTime { get; set; }
        public string Season { get; set; } = "spring";
    }
}

