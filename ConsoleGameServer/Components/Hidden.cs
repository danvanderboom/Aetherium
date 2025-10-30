using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Hidden : Component
    {
        public bool IsHidden { get; set; } = true;
        public double DiscoveryDifficulty { get; set; } = 0.5; // 0.0-1.0, where 1.0 is hardest to discover

        public Hidden() : base() { }
    }
}
