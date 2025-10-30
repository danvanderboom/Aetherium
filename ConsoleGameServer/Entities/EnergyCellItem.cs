using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
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
        }
    }
}
