using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// The on-surface exterior of a boardable vehicle (add-boardable-vehicles Phase 2): a multi-tile
    /// <see cref="Aetherium.Components.Footprint"/> entity carrying a <see cref="Aetherium.Components.Boardable"/>
    /// link to its <c>IVehicleGrain</c>. It is solid (an <c>ObstructsMovement</c> is stamped on it at
    /// placement) so players walk up to it and <c>board</c> rather than walking through it.
    /// </summary>
    public class VehicleExterior : Entity
    {
        public VehicleExterior() : base() { }
    }
}
