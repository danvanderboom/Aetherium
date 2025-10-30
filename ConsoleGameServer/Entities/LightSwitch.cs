using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class LightSwitch : Entity
    {
        public LightSwitch() : base()
        {
            // Light switch can be activated to control lighting
            Set(new Activatable
            {
                IsActivated = false,
                ToggleBehavior = true // Toggle on/off
            });

            // Light source controlled by activation state
            Set(new LightSource
            {
                Intensity = 1.0,
                Range = 8,
                IsDynamic = false,
                IsEnabled = false // Starts off
            });

            // Visual representation
            Set(new Tile { Type = TileType.None });
        }
    }
}
