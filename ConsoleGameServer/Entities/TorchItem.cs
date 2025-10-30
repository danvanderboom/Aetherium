using System;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class TorchItem : Item
    {
        public TorchItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Torch";
            carriable.Icon = "T";

            // Torch provides light while carried (IsDynamic=true)
            Set(new LightSource
            {
                Intensity = 0.8,
                Range = 4,
                IsDynamic = true,
                IsEnabled = true
            });

            // Torch has finite fuel (uses)
            Set(new Consumable
            {
                EffectType = ConsumableEffectType.EnergyRestore, // Represents fuel
                EffectValue = 1,
                Uses = 50 // 50 turns of light
            });
            
            // Torches emit moderate heat (burning fire)
            Set(new HeatSignature(0.6, TimeSpan.FromSeconds(5)));
        }
    }
}
