using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class HealthRestorativeItem : Item
    {
        public HealthRestorativeItem(int effectValue = 20) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Medkit";
            carriable.Icon = "+";

            // Restores health when consumed
            Set(new Consumable
            {
                EffectType = ConsumableEffectType.HealthRestore,
                EffectValue = effectValue,
                Uses = 1 // Single use
            });
        }
    }
}

