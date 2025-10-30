using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class BackpackItem : Item
    {
        public BackpackItem(int additionalCapacity = 5) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Backpack";
            carriable.Icon = "B";

            // Backpack increases inventory capacity when equipped
            Set(new CapacityBoost
            {
                AdditionalCapacity = additionalCapacity
            });
        }
    }
}
