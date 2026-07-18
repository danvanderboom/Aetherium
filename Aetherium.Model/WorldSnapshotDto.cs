using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// Omniscient, field-of-view-independent snapshot of a world's tiles and entities.
    /// Serialized as JSON across the management boundary
    /// (see <c>IGameManagementGrain.GetWorldSnapshotAsync</c> and <c>IGameMapGrain.GetWorldAsync</c>).
    /// </summary>
    public class WorldSnapshotDto
    {
        public string WorldId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;

        /// <summary>Bounding dimensions derived from entity locations.</summary>
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }

        /// <summary>Total entity count in the world (may exceed <see cref="Entities"/>.Count when truncated).</summary>
        public int EntityCount { get; set; }

        /// <summary>Total occupied-location count (may exceed <see cref="Tiles"/>.Count when truncated).</summary>
        public int TileCount { get; set; }

        /// <summary>True when the snapshot was capped and some entities/tiles were omitted.</summary>
        public bool Truncated { get; set; }

        public List<EntitySnapshotDto> Entities { get; set; } = new List<EntitySnapshotDto>();
        public List<TileSnapshotDto> Tiles { get; set; } = new List<TileSnapshotDto>();
    }

    public class EntitySnapshotDto
    {
        public string EntityId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();
        public List<string> Components { get; set; } = new List<string>();
    }

    public class TileSnapshotDto
    {
        public WorldLocationDto Location { get; set; } = new WorldLocationDto();
        public string Terrain { get; set; } = string.Empty;
    }
}
