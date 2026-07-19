using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Marks an entity as the boardable exterior of a vehicle (add-boardable-vehicles Phase 2) and links
    /// it to the <c>IVehicleGrain</c> that owns the vehicle's interior and voyage lifecycle. A player who
    /// perceives this entity and stands adjacent to its <see cref="Footprint"/> can <c>board</c> it, which
    /// re-points their session into the interior map the linked grain created.
    /// </summary>
    public class Boardable : Component
    {
        /// <summary>The string key of the <c>IVehicleGrain</c> that owns this vehicle — the boarding /
        /// disembarking / voyage operations dispatch to it.</summary>
        public string VehicleInstanceId { get; set; } = string.Empty;

        /// <summary>Human-readable vehicle name for board prompts (e.g. "Kestrel Dropship").</summary>
        public string DisplayName { get; set; } = string.Empty;

        public Boardable() : base() { }
    }
}
