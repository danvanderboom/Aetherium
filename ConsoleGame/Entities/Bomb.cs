using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class Bomb : Entity
    {
        public int BlastRadius { get; set; }

        public int Strength { get; set; }

        public int DetonationSeconds { get; set; }

        public DateTime? ActivationTime { get; set; }

        public Bomb() : base()
        {
        }

        public Bomb(int blastRadius, int strength, int detonationSeconds) : this()
        {
            BlastRadius = blastRadius;
            Strength = strength;
            DetonationSeconds = detonationSeconds;
        }
    }
}
