using Orleans;

namespace Aetherium.Model.Vehicles
{
    /// <summary>Snapshot of a vehicle's current state (add-boardable-vehicles).</summary>
    [GenerateSerializer]
    public class VehicleInfo
    {
        [Id(0)] public string VehicleId { get; set; } = string.Empty;
        [Id(1)] public string DisplayName { get; set; } = string.Empty;
        [Id(2)] public string? InteriorWorldId { get; set; }
        [Id(3)] public string? InteriorMapId { get; set; }
        [Id(4)] public bool Landed { get; set; }
        [Id(5)] public bool InTransit { get; set; }
        [Id(6)] public string? DockWorldId { get; set; }
        [Id(7)] public string? DockMapId { get; set; }
        [Id(8)] public int AnchorX { get; set; }
        [Id(9)] public int AnchorY { get; set; }
        [Id(10)] public int AnchorZ { get; set; }
        [Id(11)] public int PassengerCount { get; set; }
        [Id(12)] public int Capacity { get; set; }
    }

    /// <summary>Result of landing a vehicle's exterior footprint on a surface map.</summary>
    [GenerateSerializer]
    public class VehicleLandingResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Error { get; set; }
        [Id(2)] public string? MapId { get; set; }

        public static VehicleLandingResult Ok(string mapId) => new() { Success = true, MapId = mapId };
        public static VehicleLandingResult Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>Result of a board / disembark operation.</summary>
    [GenerateSerializer]
    public class VehicleBoardResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? Error { get; set; }
        /// <summary>How many players actually moved (boarded or disembarked).</summary>
        [Id(2)] public int Moved { get; set; }
        /// <summary>How many players in the manifest were rejected (over capacity, not aboard, etc.).</summary>
        [Id(3)] public int Rejected { get; set; }

        public static VehicleBoardResult Ok(int moved, int rejected = 0) =>
            new() { Success = true, Moved = moved, Rejected = rejected };
        public static VehicleBoardResult Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>What a map grain reports about a targeted boardable entity so the hub can decide whether
    /// a board request is valid (add-boardable-vehicles Phase 2).</summary>
    [GenerateSerializer]
    public class BoardableInfo
    {
        [Id(0)] public bool Found { get; set; }
        [Id(1)] public bool InReach { get; set; }
        [Id(2)] public string VehicleInstanceId { get; set; } = string.Empty;
        [Id(3)] public string DisplayName { get; set; } = string.Empty;

        public static BoardableInfo NotFound() => new() { Found = false };
    }
}
