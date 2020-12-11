using System;
using System.Drawing;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class SpaceTimeMemory
    {
        public Position Location { get; set; }

        public DateTime LastEventTime { get; set; }

        public TimeSpan TimeSinceLastSeen => DateTime.Now - LastEventTime;

        public string ContentType { get; set; }

        public string Content { get; set; }

        /// <summary>
        /// 0 ... 1
        /// </summary>
        public double Strength { get; set; }

        /// <summary>
        /// -1 ... 0 ... 1
        /// </summary>
        public double Bias { get; set; }

        public int Impressions { get; set; } = 1;
    }
}
