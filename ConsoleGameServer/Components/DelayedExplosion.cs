using System;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class DelayedExplosion : Component
    {
        public int BlastRadius { get; set; }

        public int Strength { get; set; }

        public int DetonationSeconds { get; set; }

        public DateTime? ActivationTime { get; set; }

        public DelayedExplosion() : base() { }

        public void Activate()
        {
            if (ActivationTime == null)
                ActivationTime = DateTime.Now;
        }
    }
}
