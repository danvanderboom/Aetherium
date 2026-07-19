using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model.Vehicles
{
    /// <summary>
    /// Authored, per-vehicle data (add-boardable-vehicles): binds a boardable vehicle's three pieces —
    /// its exterior footprint on a surface map, its interior map source, and the rules for landing and
    /// boarding. Pure serializable data (never hardcoded engine behavior), passed to
    /// <c>IVehicleGrain.InitializeAsync</c> so different vehicles are different data, consistent with the
    /// engine's data-vs-behavior split. See <c>docs/design/boardable-vehicles.md</c>.
    /// </summary>
    [GenerateSerializer]
    public class VehicleConfig
    {
        [Id(0)] public string VehicleId { get; set; } = string.Empty;
        [Id(1)] public string DisplayName { get; set; } = string.Empty;

        // --- Exterior footprint (tiles on a surface map, anchored at the placement WorldLocation as the
        // min corner; Width = +X extent, Length = +Y extent, Depth = +Z extent). ---
        [Id(2)] public int FootprintWidth { get; set; } = 3;
        [Id(3)] public int FootprintLength { get; set; } = 3;
        [Id(4)] public int FootprintDepth { get; set; } = 1;

        /// <summary>Terrain type stamped under the exterior footprint tiles when the ship lands (the
        /// visible hull), and restored to open ground on takeoff. Empty leaves the surface terrain as-is.</summary>
        [Id(5)] public string ExteriorTerrain { get; set; } = string.Empty;

        // --- Interior map (created via IWorldGrain.AddMapAsync on the vehicle's own world). ---
        /// <summary>The map generator that builds the interior (a registered generator-type key, e.g.
        /// "rooms-and-corridors" or "maze"). The interior is a fully-enclosed walkable area boarders spawn into.</summary>
        [Id(6)] public string InteriorGenerator { get; set; } = "rooms-and-corridors";
        [Id(7)] public int InteriorWidth { get; set; } = 24;
        [Id(8)] public int InteriorHeight { get; set; } = 24;
        [Id(9)] public int InteriorSeed { get; set; } = 1;

        // --- Landing rules. ---
        /// <summary>Surface terrain types the ship may land on (every footprint tile must be one of these
        /// and passable/unoccupied). Empty means any passable terrain is a valid landing pad.</summary>
        [Id(10)] public List<string> LandingTerrain { get; set; } = new();

        // --- Boarding. ---
        /// <summary>Maximum players allowed inside the interior at once. A boarding manifest larger than
        /// this is rejected down to capacity.</summary>
        [Id(11)] public int Capacity { get; set; } = 8;

        // --- In-transit events (Phase 4). ---
        /// <summary>Scheduled encounters that fire at offsets into a voyage and are broadcast to everyone
        /// aboard the interior (e.g. "asteroid field", "boarding party"). Empty = an uneventful trip.</summary>
        [Id(12)] public List<VoyageEventDef> InTransitEvents { get; set; } = new();
    }

    /// <summary>An authored mid-voyage encounter (add-boardable-vehicles Phase 4): what fires, when
    /// (as an offset into the voyage), and the message broadcast to passengers.</summary>
    [GenerateSerializer]
    public class VoyageEventDef
    {
        /// <summary>Minutes into the voyage at which this event becomes due.</summary>
        [Id(0)] public double OffsetMinutes { get; set; }
        [Id(1)] public string EventType { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
    }
}
