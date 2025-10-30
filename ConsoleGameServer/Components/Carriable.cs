using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Carriable : Component
    {
        public string Label { get; set; } = "Item";
        public string Icon { get; set; } = "?";
        public int Weight { get; set; } = 1;
    }
}


