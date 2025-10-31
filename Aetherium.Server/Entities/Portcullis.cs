using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class Portcullis : Entity
    {
        public Portcullis() : base()
        {
            // Heavy gate that requires mechanisms to open
            Set(new OpensAndCloses
            {
                IsOpen = false,
                IsLocked = false,
                KeyShape = string.Empty
            });

            // Blocks view and movement when closed
            Set(new ObstructsView { Opacity = 1.0 });
            Set(new ObstructsMovement { Obstruction = 1.0 });

            // Visual representation
            Set(new Tile { Type = TileType.None });
        }
    }
}


