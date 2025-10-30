using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Lighting
{
    /// <summary>
    /// Stores computed light levels for visible locations, similar to VisionFrame.
    /// Each location has a light level from 0.0 (dark) to 1.0 (fully lit).
    /// </summary>
    public class LightFrame
    {
        public ConcurrentDictionary<WorldLocation, double> LightLevels { get; protected set; }

        public LightFrame()
        {
            LightLevels = new ConcurrentDictionary<WorldLocation, double>();
        }

        public double GetLightLevel(WorldLocation location)
        {
            return LightLevels.TryGetValue(location, out var level) ? level : 0.0;
        }

        public void SetLightLevel(WorldLocation location, double level)
        {
            // Clamp to [0.0, 1.0]
            var clampedLevel = Math.Max(0.0, Math.Min(1.0, level));
            
            if (clampedLevel > 0.0)
                LightLevels.AddOrUpdate(location, clampedLevel, (key, oldValue) => clampedLevel);
            else
                LightLevels.TryRemove(location, out var _);
        }

        public void AddLightLevel(WorldLocation location, double additionalLevel)
        {
            var currentLevel = GetLightLevel(location);
            SetLightLevel(location, currentLevel + additionalLevel);
        }
    }
}

