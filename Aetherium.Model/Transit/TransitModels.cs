using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Vehicles;

namespace Aetherium.Model.Transit
{
    /// <summary>
    /// One stop on a transit line — a dock the service's train parks at so passengers board/alight
    /// (add-transit-networks Phase 1/3). A stop is a surface dock (world + map + anchor tile), mirroring
    /// how a <c>VehicleGrain</c> lands: coordinates are plain ints because <c>WorldLocation</c> is not an
    /// Orleans-serializable type and never crosses a grain boundary.
    /// </summary>
    [GenerateSerializer]
    public class TransitStop
    {
        [Id(0)] public string DockWorldId { get; set; } = string.Empty;
        [Id(1)] public string DockMapId { get; set; } = string.Empty;
        [Id(2)] public int AnchorX { get; set; }
        [Id(3)] public int AnchorY { get; set; }
        [Id(4)] public int AnchorZ { get; set; }
        /// <summary>Display name of the stop (usually the settlement it serves).</summary>
        [Id(5)] public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Authored, per-line data for a scheduled transit service (add-transit-networks Phase 3): the ordered
    /// list of stops, the timings, and the train that runs the line. A <c>TransitServiceGrain</c> drives a
    /// <see cref="Train"/> (a boardable <c>VehicleGrain</c>) around these <see cref="Stops"/> — dwelling at
    /// each, then departing to the next on a timed voyage — reusing the whole boardable-vehicles voyage
    /// machinery. Pure serializable data, consistent with the engine's data-vs-behavior split.
    /// </summary>
    [GenerateSerializer]
    public class TransitServiceConfig
    {
        [Id(0)] public string LineId { get; set; } = string.Empty;
        [Id(1)] public string DisplayName { get; set; } = string.Empty;

        /// <summary>The ordered stops the service visits. A real line has two or more.</summary>
        [Id(2)] public List<TransitStop> Stops { get; set; } = new();

        /// <summary>Voyage time between consecutive stops, in minutes (the "how long is the ride").</summary>
        [Id(3)] public double HopMinutes { get; set; } = 15.0;

        /// <summary>How long the train dwells at a station before departing to the next stop, in minutes
        /// (the "wait at the platform" window during which passengers board/alight).</summary>
        [Id(4)] public double DwellMinutes { get; set; } = 1.0;

        /// <summary>When true, the service loops back to the first stop after the last (a circular line);
        /// when false it terminates at the last stop.</summary>
        [Id(5)] public bool Loop { get; set; } = true;

        /// <summary>The boardable vehicle that runs the line — its footprint, interior, and capacity.</summary>
        [Id(6)] public VehicleConfig Train { get; set; } = new();
    }

    /// <summary>A snapshot of a running transit service, for inspection/HUD.</summary>
    [GenerateSerializer]
    public class TransitServiceInfo
    {
        [Id(0)] public string LineId { get; set; } = string.Empty;
        [Id(1)] public string? TrainId { get; set; }
        [Id(2)] public int StopCount { get; set; }
        [Id(3)] public int CurrentStopIndex { get; set; }
        [Id(4)] public string CurrentStopName { get; set; } = string.Empty;
        [Id(5)] public bool InTransit { get; set; }
        [Id(6)] public bool Started { get; set; }
    }
}
