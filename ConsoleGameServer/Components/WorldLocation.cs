using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class WorldLocation : Component
    {
        public static WorldLocation None = new WorldLocation(0, 0, 0) { IsNone = true };

        int _X;
        public int X 
        {
            get => _X;
            set
            {
                _X = value;
                IsNone = false;
            }
        }

        int _Y;
        public int Y
        {
            get => _Y;
            set
            {
                _Y = value;
                IsNone = false;
            }
        }

        int _Z;
        public int Z
        {
            get => _Z;
            set
            {
                _Z = value;
                IsNone = false;
            }
        }

        public bool IsNone { get; protected set; }

        public WorldLocation() 
        {
            IsNone = true;
        }

        public WorldLocation(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as WorldLocation;
            if (other is null)
                return false;

            return Equals(this, other);
        }

        public static bool Equals(WorldLocation? rhs, WorldLocation? lhs)
        {
            //if (rhs is null && lhs is null)
            //    return true;

            if (rhs is null || lhs is null)
                return false;

            return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public static bool operator ==(WorldLocation? lhs, WorldLocation? rhs) => Equals(lhs, rhs);

        public static bool operator !=(WorldLocation? lhs, WorldLocation? rhs) => !Equals(lhs, rhs);

        public override string ToString() => $"x: {X}, y: {Y}, z: {Z}";

        public List<int> ToList() => new List<int> { X, Y, Z };

        public static WorldLocation FromCoordinates(IList<int> loc) => new WorldLocation(loc[0], loc[1], loc[2]);
    }
}
