using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// Full snapshot of a region's state for serialization/deserialization.
    /// </summary>
    [GenerateSerializer]
    public class RegionStateSnapshot
    {
        [Id(0)] public string RegionId { get; set; } = string.Empty;
        [Id(1)] public string MapId { get; set; } = string.Empty;
        [Id(2)] public int RegionX { get; set; }
        [Id(3)] public int RegionY { get; set; }
        [Id(4)] public int ZLevel { get; set; }
        [Id(5)] public int RegionSize { get; set; }
        [Id(6)] public DateTime SavedAt { get; set; }
        [Id(7)] public double GameTimeHours { get; set; }
        
        /// <summary>
        /// Serialized world entities in this region (JSON or binary).
        /// </summary>
        [Id(8)] public byte[]? SerializedEntities { get; set; }
        
        /// <summary>
        /// Terrain modifications: location -> terrain type name.
        /// </summary>
        [Id(9)] public Dictionary<string, string> TerrainModifications { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Traversal heatmap: location -> traversal count.
        /// </summary>
        [Id(10)] public Dictionary<string, int> TraversalHeatmap { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// Agent-built structures: location -> structure type.
        /// </summary>
        [Id(11)] public Dictionary<string, string> BuiltStructures { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Weather state at save time.
        /// </summary>
        [Id(12)] public string? WeatherType { get; set; }
        
        /// <summary>
        /// Season at save time.
        /// </summary>
        [Id(13)] public string? Season { get; set; }
    }

    /// <summary>
    /// Incremental delta update for a region.
    /// </summary>
    [GenerateSerializer]
    public class RegionDelta
    {
        [Id(0)] public string RegionId { get; set; } = string.Empty;
        [Id(1)] public DateTime Timestamp { get; set; }
        [Id(2)] public DeltaType Type { get; set; }
        [Id(3)] public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    [GenerateSerializer]
    public enum DeltaType
    {
        TerrainModified,
        EntityAdded,
        EntityRemoved,
        EntityMoved,
        TraversalRecorded,
        StructureBuilt,
        WeatherChanged,
        Other
    }
}

