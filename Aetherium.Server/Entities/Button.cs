using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class Button : Entity
    {
        public Button(bool toggleBehavior = false) : base()
        {
            // Button that can be pressed
            Set(new Activatable
            {
                IsActivated = false,
                ToggleBehavior = toggleBehavior, // Can be one-time or toggle
                TargetEntityIds = new System.Collections.Generic.List<string>()
            });

            // Visual representation
            Set(new Tile { Type = TileType.None });
        }
    }
}


