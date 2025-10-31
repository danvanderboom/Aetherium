using Aetherium.Core;

namespace Aetherium.Components
{
    public class ForcesDoor : Component
    {
        public int Strength { get; set; } = 1;
        public int Durability { get; set; } = 10;

        public ForcesDoor() : base() { }
    }
}


