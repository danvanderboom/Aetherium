using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class ForcesDoor : Component
    {
        public int Strength { get; set; } = 1;
        public int Durability { get; set; } = 10;

        public ForcesDoor() : base() { }
    }
}

