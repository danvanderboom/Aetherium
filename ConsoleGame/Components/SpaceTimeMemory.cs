using System;
using System.Drawing;

namespace ConsoleGame.Components
{
    public class SpaceTimeMemory
    {
        public WorldLocation Location { get; set; } = WorldLocation.None;

        public DateTime LastEventTime { get; set; }

        public TimeSpan TimeSinceLastSeen => DateTime.Now - LastEventTime;

        public string ContentType { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty; // PerceptionFrame object?
        //public PerceptionFrame Content { get; set; } // TODO

        /// <summary>
        /// 0 ... 1
        /// </summary>
        public double Strength { get; set; } = 0.5;

        /// <summary>
        /// -1 ... 0 ... 1
        /// </summary>
        public double Bias { get; set; } = 0.5;

        public int Impressions { get; set; } = 1;
    }
}
