using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Position : Component
    {
        public static Position Empty = new Position(0, 0, 0) { IsEmpty = true };

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

        public Position() { }

        public Position(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Position;
            if (other is null)
                return false;

            return Equals(this, other);
        }

        public static bool Equals(Position rhs, Position lhs)
        {
            if (rhs is null && lhs is null)
                return true;

            if (rhs is null || lhs is null)
                return false;

            return lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;
        }

        public override int GetHashCode() => $"{X}-{Y}-{Z}".GetHashCode();

        public static bool operator ==(Position lhs, Position rhs) => Equals(lhs, rhs);

        public static bool operator !=(Position lhs, Position rhs) => !Equals(lhs, rhs);
    }
}
