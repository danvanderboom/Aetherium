using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class MapItem : Item
    {
        public MapItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Map";
            carriable.Icon = "M";

            // Provides navigation assistance
            Set(new ProvidesNavigation
            {
                RevealsArea = true, // Reveals explored areas
                DirectionToTarget = null // Can be set to point to goal
            });
        }
    }
}

