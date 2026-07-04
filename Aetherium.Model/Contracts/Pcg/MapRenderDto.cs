using System;
using System.Collections.Generic;

namespace Aetherium.Model.Pcg
{
    /// <summary>
    /// Compact map rendering data for preview visualization.
    /// </summary>
    public sealed class MapRenderDto
    {
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// Compact tile array: byte per tile (tile type enum value).
        /// Row-major order: tiles[y * Width + x].
        /// </summary>
        public byte[] Tiles { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Overlay data for rooms.
        /// </summary>
        public List<RoomOverlay> Rooms { get; set; } = new List<RoomOverlay>();

        /// <summary>
        /// Overlay data for corridors.
        /// </summary>
        public List<CorridorOverlay> Corridors { get; set; } = new List<CorridorOverlay>();

        /// <summary>
        /// Overlay data for anchors.
        /// </summary>
        public List<AnchorOverlay> Anchors { get; set; } = new List<AnchorOverlay>();

        /// <summary>
        /// Overlay data for regions/biomes.
        /// </summary>
        public List<RegionOverlay> Regions { get; set; } = new List<RegionOverlay>();

        /// <summary>
        /// Palette information for tile types.
        /// </summary>
        public Dictionary<byte, TileInfo> Palette { get; set; } = new Dictionary<byte, TileInfo>();
    }

    public sealed class RoomOverlay
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Label { get; set; }
    }

    public sealed class CorridorOverlay
    {
        public List<Point> Points { get; set; } = new List<Point>();
        public string? Label { get; set; }
    }

    public sealed class AnchorOverlay
    {
        public HybridAnchor Anchor { get; set; } = null!;
        public string? Label { get; set; }
    }

    public sealed class RegionOverlay
    {
        public List<Point> Points { get; set; } = new List<Point>();
        public string RegionType { get; set; } = string.Empty;
        public string? Label { get; set; }
    }

    public sealed class TileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string? Color { get; set; }
    }

    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}

