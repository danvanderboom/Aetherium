using System;
using System.Drawing;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Goal : Component
    {
        public DateTime Created { get; set; }

        public Location Location { get; set; }
    }
}
