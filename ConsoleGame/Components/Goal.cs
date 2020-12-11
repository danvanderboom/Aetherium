using System;
using System.Drawing;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame
{
    public class Goal : Component
    {
        public DateTime Created { get; set; }

        public Position Location { get; set; }
    }
}
