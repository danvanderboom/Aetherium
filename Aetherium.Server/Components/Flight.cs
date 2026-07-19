using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Flight state machine. Airborne entities occupy air bands and ignore ground-band obstruction;
    /// landing/takeoff transition to/from the surface on valid terrain.
    /// </summary>
    public enum FlightState
    {
        Airborne,
        TakingOff,
        Landing,
        Landed
    }

    /// <summary>
    /// Grants an entity band freedom: while <see cref="State"/> is <see cref="FlightState.Airborne"/> it may
    /// occupy altitude bands in [<see cref="MinBand"/>, <see cref="MaxBand"/>], traverse over ground-band
    /// obstruction, and change altitude without CanAscend/CanDescend markers.
    ///
    /// Phase 1 introduces the component as data (used to guard the band-aware passability path); the airborne
    /// movement wiring, spawn attachment, and flight-plan follower arrive in later phases.
    /// </summary>
    public class Flight : Component
    {
        /// <summary>Lowest air band the entity may occupy while airborne.</summary>
        public int MinBand { get; set; } = 1;

        /// <summary>Ceiling band (e.g. orbit for a satellite).</summary>
        public int MaxBand { get; set; } = 5;

        /// <summary>Preferred cruising altitude band.</summary>
        public int CruiseBand { get; set; } = 2;

        /// <summary>Whether this flyer can land and take off (gated on valid terrain in later phases).</summary>
        public bool CanLand { get; set; } = false;

        /// <summary>
        /// Terrain type names this flyer may set down on, so landability depends on the flyer: a bird lands on
        /// forest/mountain/water, a floatplane on water/plains, a wheeled plane only on road/plains. When empty,
        /// landing falls back to the world's <see cref="Aetherium.Core.World.LandingTerrainNames"/>. This gates
        /// only <em>terrain</em> surfaces; a flyer may still come to rest on top of a structure below it.
        /// </summary>
        public HashSet<string> LandableTerrain { get; set; } = new HashSet<string>();

        public FlightState State { get; set; } = FlightState.Airborne;

        public Flight() : base() { }
    }
}
