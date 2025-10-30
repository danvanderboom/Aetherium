using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class CompassItem : Item
    {
        public CompassItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Compass";
            carriable.Icon = "N";

            // Provides direction to target
            Set(new ProvidesNavigation
            {
                RevealsArea = false,
                DirectionToTarget = null // Points to target when set
            });
        }
    }
}
