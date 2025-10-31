using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class CapacityBoost : Component
    {
        public int AdditionalCapacity { get; set; } = 5;

        public CapacityBoost() : base() { }
    }
}

