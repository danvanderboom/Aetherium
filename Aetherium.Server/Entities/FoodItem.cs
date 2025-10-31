using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class FoodItem : Item
    {
        public FoodItem(int uses = 5) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Rations";
            carriable.Icon = "F";

            // Restores hunger (can be consumed multiple times)
            Set(new Consumable
            {
                EffectType = ConsumableEffectType.HungerRestore,
                EffectValue = 10,
                Uses = uses // Multiple uses from a package
            });
        }
    }
}


