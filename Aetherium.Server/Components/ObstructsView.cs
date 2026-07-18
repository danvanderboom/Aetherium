using System;
using System.Linq;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class ObstructsView : Component
    {
        public double Opacity { get; set; } = 1;

        /// <summary>
        /// How many altitude bands upward from the entity's own band this sight obstruction spans.
        /// Blocks sight in bands [band, band + Height). Default 1 = its own band only.
        /// Opacity is independent: a glass skylight sets Opacity = 0 (movement may still be blocked),
        /// so observers see through it to bands above.
        /// </summary>
        public int Height { get; set; } = 1;

        public ObstructsView() : base() { }
    }
}

