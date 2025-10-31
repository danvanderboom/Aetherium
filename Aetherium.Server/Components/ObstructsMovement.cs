using System;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class ObstructsMovement : Component
    {
        public double Obstruction { get; set; } = 1;

        public ObstructsMovement() : base() { }
    }
}

