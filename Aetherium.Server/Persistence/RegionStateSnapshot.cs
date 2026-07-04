using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// A persisted heat trail — the durable projection of one entry in the grain's
    /// <see cref="Aetherium.Server.Perception.HeatTrailTracker"/>. Serialized as part of the
    /// region snapshot so grain-authoritative heat survives a cold start (P3-8). Coordinates are
    /// stored as primitives (like the wire <c>MapDelta</c>s) because <c>WorldLocation</c> has no
    /// Orleans codec.
    /// </summary>
    [GenerateSerializer]
    public class PersistedHeatTrail
    {
        [Id(0)] public int X { get; set; }
        [Id(1)] public int Y { get; set; }
        [Id(2)] public int Z { get; set; }
        [Id(3)] public string EntityId { get; set; } = string.Empty;
        [Id(4)] public DateTime Timestamp { get; set; }
        [Id(5)] public double BaseIntensity { get; set; }
        [Id(6)] public TimeSpan Duration { get; set; }
    }

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

        /// <summary>
        /// Highest grain-emitted <c>MapDelta.Sequence</c> incorporated into this snapshot.
        /// On cold start, the grain loads the snapshot and replays only deltas with
        /// <c>Sequence &gt; LastSequence</c>. Zero on first snapshot or pre-versioned data.
        /// </summary>
        [Id(14)] public long LastSequence { get; set; }

        /// <summary>
        /// Grain-authoritative heat trails captured at save time, so the per-cell heat map
        /// (infrared/heat-vision) survives a cold start instead of resetting to empty (P3-8).
        /// Null on snapshots written before this field existed. Stored inside the Orleans
        /// snapshot blob, so no store schema change is required.
        /// </summary>
        [Id(15)] public List<PersistedHeatTrail>? HeatTrails { get; set; }
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

