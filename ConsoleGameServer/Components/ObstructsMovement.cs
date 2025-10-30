using System;
using System.Linq;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class ObstructsMovement : Component
    {
        public double Obstruction { get; set; } = 1;

        public ObstructsMovement() : base() { }
    }
}
