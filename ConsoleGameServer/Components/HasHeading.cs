using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class HasHeading : Component
    {
        public float Heading { get; set; }

        public WorldDirection ToWorldDirection() => Heading switch
        {
            0 => WorldDirection.North,
            90 => WorldDirection.East,
            180 => WorldDirection.South,
            270 => WorldDirection.West,
            _ => throw new InvalidOperationException("Invalid heading")
        };
    }
}
