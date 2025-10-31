using System;
using System.Drawing;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class Goal : Component
    {
        public DateTime Created { get; set; }

        public WorldLocation Location { get; set; } = WorldLocation.None;
    }
}

