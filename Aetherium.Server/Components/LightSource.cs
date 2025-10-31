using System;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class LightSource : Component
    {
        /// <summary>
        /// Maximum light intensity emitted by this source (0.0-1.0)
        /// </summary>
        public double Intensity { get; set; } = 1.0;

        /// <summary>
        /// Maximum range in cells that light can travel
        /// </summary>
        public int Range { get; set; } = 5;

        /// <summary>
        /// Red component of light color (0.0-1.0)
        /// </summary>
        public double Red { get; set; } = 1.0;

        /// <summary>
        /// Green component of light color (0.0-1.0)
        /// </summary>
        public double Green { get; set; } = 1.0;

        /// <summary>
        /// Blue component of light color (0.0-1.0)
        /// </summary>
        public double Blue { get; set; } = 1.0;

        /// <summary>
        /// Whether this light source is currently enabled/emitting light
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether this light source moves (attached to entity movement)
        /// </summary>
        public bool IsDynamic { get; set; } = false;

        public LightSource() : base() { }

        public LightSource(double intensity, int range) : this()
        {
            Intensity = intensity;
            Range = range;
        }

        public LightSource(double intensity, int range, double red, double green, double blue) 
            : this(intensity, range)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
    }
}


