using Aetherium.Core;

namespace Aetherium.Components
{
    public class ProvidesNavigation : Component
    {
        public bool RevealsArea { get; set; } = false;
        public WorldLocation? DirectionToTarget { get; set; } = null;

        public ProvidesNavigation() : base() { }
    }
}


