using System;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class HeatSignature : Component
    {
        public double Intensity { get; set; }
        public TimeSpan Duration { get; set; }

        public HeatSignature(double intensity, TimeSpan duration)
        {
            // Clamp intensity to 0.0-1.0 range
            Intensity = Math.Max(0.0, Math.Min(1.0, intensity));
            Duration = duration;
        }
    }
}
