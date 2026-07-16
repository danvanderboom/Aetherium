using System;

namespace Aetherium.Client
{
    /// <summary>
    /// A cell in CLIENT-space — the stable coordinate frame the PerceptionStore synthesizes
    /// via anchoring. The server never reveals absolute world coordinates (a deliberate
    /// fairness constraint); client-space starts at the join position = (0,0,0) and stays
    /// stable across frames so views can tween, remember terrain, and draw minimaps.
    /// </summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public GridPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static GridPoint Origin => new GridPoint(0, 0, 0);

        public GridPoint Offset(int dx, int dy, int dz) => new GridPoint(X + dx, Y + dy, Z + dz);

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X, Y, Z).GetHashCode();
        public static bool operator ==(GridPoint a, GridPoint b) => a.Equals(b);
        public static bool operator !=(GridPoint a, GridPoint b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
