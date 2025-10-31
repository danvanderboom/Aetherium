using System;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.WorldGen.Hybrid
{
    /// <summary>
    /// Represents a hand-placed anchor that PCG must respect during generation.
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
        /// Polygon vertices for polygon type.
        /// </summary>
        public List<WorldLocation>? Vertices { get; set; }

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

        /// <summary>
        /// Checks if a location is contained within this anchor.
        /// </summary>
        public bool Contains(WorldLocation location, int zLevel)
        {
            if (zLevel != ZLevel)
                return false;

            return Type switch
            {
                AnchorType.Point => location.X == X && location.Y == Y,
                AnchorType.Rectangle => Width.HasValue && Height.HasValue &&
                    location.X >= X && location.X < X + Width.Value &&
                    location.Y >= Y && location.Y < Y + Height.Value,
                AnchorType.Polygon => Vertices != null && IsPointInPolygon(location, Vertices),
                _ => false
            };
        }

        /// <summary>
        /// Gets all locations contained by this anchor.
        /// </summary>
        public IEnumerable<WorldLocation> GetLocations(int zLevel)
        {
            if (zLevel != ZLevel)
                yield break;

            switch (Type)
            {
                case AnchorType.Point:
                    yield return new WorldLocation(X, Y, zLevel);
                    break;

                case AnchorType.Rectangle when Width.HasValue && Height.HasValue:
                    for (var y = Y; y < Y + Height.Value; y++)
                    {
                        for (var x = X; x < X + Width.Value; x++)
                        {
                            yield return new WorldLocation(x, y, zLevel);
                        }
                    }
                    break;

                case AnchorType.Polygon when Vertices != null:
                    // For polygon, enumerate all points within bounding box
                    var minX = int.MaxValue;
                    var maxX = int.MinValue;
                    var minY = int.MaxValue;
                    var maxY = int.MinValue;

                    foreach (var vertex in Vertices)
                    {
                        minX = Math.Min(minX, vertex.X);
                        maxX = Math.Max(maxX, vertex.X);
                        minY = Math.Min(minY, vertex.Y);
                        maxY = Math.Max(maxY, vertex.Y);
                    }

                    for (var y = minY; y <= maxY; y++)
                    {
                        for (var x = minX; x <= maxX; x++)
                        {
                            var loc = new WorldLocation(x, y, zLevel);
                            if (IsPointInPolygon(loc, Vertices))
                            {
                                yield return loc;
                            }
                        }
                    }
                    break;
            }
        }

        private static bool IsPointInPolygon(WorldLocation point, List<WorldLocation> vertices)
        {
            // Ray casting algorithm
            var n = vertices.Count;
            if (n < 3)
                return false;

            var inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = vertices[i].X;
                var yi = vertices[i].Y;
                var xj = vertices[j].X;
                var yj = vertices[j].Y;

                var intersect = ((yi > point.Y) != (yj > point.Y)) &&
                               (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }
    }

    public enum AnchorType
    {
        Point,
        Rectangle,
        Polygon
    }
}

