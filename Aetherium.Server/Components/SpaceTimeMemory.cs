using System;
using System.Drawing;

namespace Aetherium.Components
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

        /// <summary>
        /// This memory's own decay half-life in seconds (add-memory-dynamics). Grows on spaced
        /// reinforcement so frequently revisited content decays more slowly. <c>0</c> means "never
        /// reinforced" — reads fall back to the character's effective base half-life, so legacy rows
        /// and dynamics-off worlds behave exactly as before.
        /// </summary>
        public double StabilitySeconds { get; set; } = 0;

        /// <summary>
        /// Once stability reaches the world's permanence threshold this latches true and the memory
        /// never decays or culls again — "so familiar it stays forever."
        /// </summary>
        public bool Permanent { get; set; } = false;
    }
}

