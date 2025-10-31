using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class RopeItem : Item
    {
        public RopeItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Rope";
            carriable.Icon = "~";

            // Rope can be used to create climbable locations
            Set(new Climbable
            {
                Direction = ClimbDirection.Both, // Can climb up or down
                RequiresItem = false, // Once placed, doesn't require item to use
                RequiredItemId = null
            });
        }
    }
}

