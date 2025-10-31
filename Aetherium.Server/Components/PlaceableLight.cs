using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Marker component for light sources that can be placed in the world.
    /// Used in conjunction with LightSource component.
    /// </summary>
    public class PlaceableLight : Component
    {
        public bool IsPlaced { get; set; } = false;

        public PlaceableLight() : base() { }
    }
}


