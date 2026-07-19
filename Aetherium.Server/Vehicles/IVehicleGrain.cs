using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Aetherium.Model.Vehicles;

namespace Aetherium.Server.Vehicles
{
    /// <summary>
    /// Owns one boardable vehicle (add-boardable-vehicles): its interior map (a map on the vehicle's own
    /// world, created at <see cref="InitializeAsync"/>), its landed exterior footprint on a surface map,
    /// the boarding manifest, and — from Phase 3 — the voyage lifecycle. Modeled on
    /// <c>IDungeonInstanceGrain</c> (an instance grain whose map is the ship interior). Keyed by a
    /// vehicle-instance GUID string.
    /// </summary>
    public interface IVehicleGrain : IGrainWithStringKey
    {
        /// <summary>Creates the vehicle's interior — a dedicated world whose "Main" map is built by
        /// <see cref="VehicleConfig.InteriorGenerator"/> — and stores the config. Idempotent: a second
        /// call with an already-initialized grain is a no-op.</summary>
        Task InitializeAsync(VehicleConfig config);

        /// <summary>Places (parks) the exterior footprint on <paramref name="surfaceMapId"/> at the anchor
        /// tile (<paramref name="anchorX"/>, <paramref name="anchorY"/>, <paramref name="anchorZ"/>),
        /// validating every footprint tile against the config's landing rules. Records the dock so boarders
        /// know which surface to leave and disembarkers where to return.</summary>
        Task<VehicleLandingResult> LandAsync(string surfaceWorldId, string surfaceMapId, int anchorX, int anchorY, int anchorZ);

        /// <summary>Removes the exterior footprint from its current dock surface (takeoff). No-op when not
        /// landed. Passengers already inside the interior are unaffected.</summary>
        Task TakeOffAsync();

        /// <summary>Boards a manifest into the interior: each player (up to remaining capacity) is joined
        /// onto the interior map, left from the dock surface, and — if they have a live session — has that
        /// session re-pointed into the interior with a fresh perception frame. Surplus over capacity is
        /// rejected. Requires the vehicle to be landed.</summary>
        Task<VehicleBoardResult> BoardAsync(IReadOnlyList<string> playerIds);

        /// <summary>Disembarks passengers onto the current dock surface (the reverse of
        /// <see cref="BoardAsync"/>). Requires the vehicle to be landed.</summary>
        Task<VehicleBoardResult> DisembarkAsync(IReadOnlyList<string> playerIds);

        /// <summary>Ticks the interior map so gameplay inside proceeds whether parked or (Phase 3) in
        /// transit — mirrors <c>DungeonInstanceGrain.TickAsync</c>.</summary>
        Task TickAsync(System.TimeSpan gameTimeElapsed);

        /// <summary>Takes off from the current dock and starts a timed voyage to a destination surface
        /// (add-boardable-vehicles Phase 3): removes the exterior footprint from the origin, records an
        /// ETA <paramref name="voyageMinutes"/> from now, marks the vehicle in transit, and arms an
        /// Orleans reminder that self-drives arrival. Passengers stay in the interior, which keeps
        /// ticking en route. Requires the vehicle to be landed.</summary>
        Task<VoyageResult> DepartAsync(string destinationWorldId, string destinationMapId,
            int destAnchorX, int destAnchorY, int destAnchorZ, double voyageMinutes);

        /// <summary>One voyage step (called by the voyage reminder, and directly by tests): if the ETA has
        /// passed, place the exterior at the destination dock, unregister the reminder, and transition to
        /// landed; otherwise tick the interior and push a voyage-progress update to passengers. No-op when
        /// not in transit.</summary>
        Task TickVoyageAsync();

        Task<VehicleInfo> GetInfoAsync();
        Task<string?> GetInteriorMapIdAsync();
        Task<IReadOnlyList<string>> GetPassengersAsync();
    }
}
