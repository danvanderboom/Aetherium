using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class PressureSensitive : Component
    {
        public int WeightThreshold { get; set; } = 1;
        public bool IsPressed { get; set; } = false;
        public List<string> TargetEntityIds { get; set; } = new List<string>();

        public PressureSensitive() : base() { }
    }
}

