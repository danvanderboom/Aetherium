using System;
using System.Collections.Generic;

namespace Aetherium.Model.Pcg
{
    /// <summary>
    /// Represents a hand-placed anchor that PCG must respect.
    /// </summary>
    public sealed class HybridAnchor
    {
        /// <summary>
        /// Anchor type: point, rectangle, or polygon.
        /// </summary>
        public AnchorType Type { get; set; }

        /// <summary>
        /// X coordinate for point, or min X for rectangle.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate for point, or min Y for rectangle.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width for rectangle (ignored for point).
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Height for rectangle (ignored for point).
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Polygon vertices for polygon type (list of {x, y} pairs).
        /// </summary>
        public List<Point>? Vertices { get; set; }

        /// <summary>
        /// Semantic tags for the anchor (e.g., "entrance", "boss-room", "treasure").
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Whether this anchor blocks generation (true) or requires content (false).
        /// </summary>
        public bool IsBlocking { get; set; } = false;

        /// <summary>
        /// Z-level for multi-level worlds.
        /// </summary>
        public int ZLevel { get; set; } = 0;

        /// <summary>
        /// Priority for conflict resolution (higher = more important).
        /// </summary>
        public int Priority { get; set; } = 0;

        public struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }
        }
    }

    public enum AnchorType
    {
        Point,
        Rectangle,
        Polygon
    }
}

