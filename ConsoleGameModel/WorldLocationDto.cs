using System;

namespace ConsoleGameModel
{
    public class WorldLocationDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public WorldLocationDto()
        {
        }

        public WorldLocationDto(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as WorldLocationDto;
            if (other is null)
                return false;

            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}


