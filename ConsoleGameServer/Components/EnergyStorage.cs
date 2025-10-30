using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class EnergyStorage : Component
    {
        public int EnergyLevel { get; set; } = 100;
        public int MaxEnergy { get; set; } = 100;
        public int ConsumesPerUse { get; set; } = 1;

        public EnergyStorage() : base() { }
    }
}
