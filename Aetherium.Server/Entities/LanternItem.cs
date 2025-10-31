using System;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class LanternItem : Item
    {
        public LanternItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Lantern";
            carriable.Icon = "L";

            // Lantern provides brighter, longer-range light
            Set(new LightSource
            {
                Intensity = 1.0,
                Range = 6,
                IsDynamic = true,
                IsEnabled = true
            });

            // Lantern has more fuel than torch
            Set(new Consumable
            {
                EffectType = ConsumableEffectType.EnergyRestore,
                EffectValue = 1,
                Uses = 100 // 100 turns of light
            });
            
            // Lanterns emit moderate heat (burning oil)
            Set(new HeatSignature(0.5, TimeSpan.FromSeconds(5)));
        }
    }
}


