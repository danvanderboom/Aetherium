using System;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class LightCrystal : Item
    {
        public LightCrystal() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Light Crystal";
            carriable.Icon = "C";

            // Can be placed as a light source
            Set(new PlaceableLight
            {
                IsPlaced = false
            });

            // Emits light when placed
            Set(new LightSource
            {
                Intensity = 0.9,
                Range = 5,
                IsDynamic = false,
                IsEnabled = false // Enabled when placed
            });
            
            // Crystals emit low heat (magical/crystal energy)
            Set(new HeatSignature(0.3, TimeSpan.FromSeconds(3)));
        }
    }
}
