using System;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Server.Simulation;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Generation pass that initializes temporal state for regions:
    /// weather patterns, seasonal biomes, and initial time-based modifiers.
    /// </summary>
    public sealed class TemporalInitPass : IWorldGenerationPass
    {
        public string Name => "temporal-init";
        public GenerationPhase Phase => GenerationPhase.Population;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Works with all templates

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Temporal init pass requires world instance");
                return;
            }

            var world = context.World;
            var rng = context.GeneratorContext.GetRandom("temporal-init");

            // Initialize temporal state metadata in the shared data
            // This will be used by regions when they're initialized
            context.SharedData["temporal:initialDay"] = 0;
            context.SharedData["temporal:initialTimeOfDay"] = 12.0; // Noon
            
            // Get world dimensions from request
            var width = context.Request.Width;
            var height = context.Request.Height;
            var depth = context.Request.Levels;
            
            // Seed weather patterns per region (64x64 chunks)
            var regionSize = 64;
            var regionsX = (width + regionSize - 1) / regionSize;
            var regionsY = (height + regionSize - 1) / regionSize;
            
            var weatherPatterns = new Dictionary<string, WeatherType>();
            
            for (int z = 0; z < depth; z++)
            {
                for (int regionY = 0; regionY < regionsY; regionY++)
                {
                    for (int regionX = 0; regionX < regionsX; regionX++)
                    {
                        var regionKey = $"region:{regionX},{regionY},{z}";
                        
                        // Generate initial weather based on location (e.g., higher altitude = snow, coastal = rain)
                        var weather = GenerateInitialWeather(context, regionX, regionY, z, rng);
                        weatherPatterns[regionKey] = weather;
                    }
                }
            }
            
            context.SharedData["temporal:weatherPatterns"] = weatherPatterns;
        }

        private WeatherType GenerateInitialWeather(
            WorldGenerationContext context,
            int regionX,
            int regionY,
            int zLevel,
            Random rng)
        {
            // Simple weather generation based on region location
            // In a full implementation, this would consider elevation, biomes, etc.
            
            var value = rng.NextDouble();
            
            if (zLevel > 0) // Underground or elevated
                return WeatherType.Clear; // No weather underground
            
            if (value < 0.5)
                return WeatherType.Clear;
            else if (value < 0.75)
                return WeatherType.Rainy;
            else if (value < 0.9)
                return WeatherType.Foggy;
            else
                return WeatherType.Stormy;
        }
    }
}

