using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class PressurePlate : Entity
    {
        public PressurePlate() : base()
        {
            // Pressure plate that activates when stepped on
            Set(new PressureSensitive
            {
                WeightThreshold = 1, // Any entity triggers it
                IsPressed = false,
                TargetEntityIds = new System.Collections.Generic.List<string>()
            });

            // Can activate target entities
            Set(new Activatable
            {
                IsActivated = false,
                ToggleBehavior = false // One-time activation or resets when weight removed
            });

            // Visual representation (usually floor tile)
            Set(new Tile { Type = TileType.None });
        }
    }
}
