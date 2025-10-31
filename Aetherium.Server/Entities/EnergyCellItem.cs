using System;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class EnergyCellItem : Item
    {
        public EnergyCellItem(int energyLevel = 100) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Energy Cell";
            carriable.Icon = "E";

            // Stores energy for powering devices
            Set(new EnergyStorage
            {
                EnergyLevel = energyLevel,
                MaxEnergy = 100,
                ConsumesPerUse = 1
            });
            
            // Energy cells emit low heat (electrical energy)
            Set(new HeatSignature(0.2, TimeSpan.FromSeconds(3)));
        }
    }
}

