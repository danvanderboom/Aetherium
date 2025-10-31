using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Activatable : Component
    {
        public bool IsActivated { get; set; } = false;
        public List<string> TargetEntityIds { get; set; } = new List<string>();
        public bool ToggleBehavior { get; set; } = false; // false = one-time, true = toggle

        public Activatable() : base() { }
    }
}

