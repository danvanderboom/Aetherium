using System;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class ObstructsMovement : Component
    {
        public double Obstruction { get; set; } = 1;

        /// <summary>
        /// How many altitude bands upward from the entity's own band this obstruction blocks.
        /// The obstruction blocks movement in bands [band, band + Height). Default 1 = its own band only.
        /// A tall structure (wall, mountain, building) uses a larger value so flyers above it are clear.
        /// </summary>
        public int Height { get; set; } = 1;

        public ObstructsMovement() : base() { }
    }
}

