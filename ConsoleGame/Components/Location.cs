using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Location : Component
    {
        public static Location Empty = new Location(0, 0, 0) { IsEmpty = true };

        int _X;
        public int X 
        {
            get => _X;
            set
            {
                _X = value;
                IsEmpty = false;
            }
        }

        int _Y;
        public int Y
        {
            get => _Y;
            set
            {
                _Y = value;
                IsEmpty = false;
            }
        }

        int _Z;
        public int Z
        {
            get => _Z;
            set
            {
                _Z = value;
                IsEmpty = false;
            }
        }

        public bool IsEmpty { get; protected set; }

        public Location() 
        {
            IsEmpty = true;
        }

        public Location(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Location;
            if (other is null)
                return false;

            return Equals(this, other);
        }

        public static bool Equals(Location rhs, Location lhs)
        {
            if (rhs is null && lhs is null)
                return true;

            if (rhs is null || lhs is null)
                return false;

            return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        }

        public override int GetHashCode() => ToString().GetHashCode();

        public static bool operator ==(Location lhs, Location rhs) => Equals(lhs, rhs);

        public static bool operator !=(Location lhs, Location rhs) => !Equals(lhs, rhs);

        public override string ToString() => $"{X}, {Y}, {Z}";
    }
}
