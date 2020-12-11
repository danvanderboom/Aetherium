using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleGame.Core
{
    public class Size3d
    {
        int _Width;
        public int Width
        {
            get => _Width;
            set
            {
                _Width = value;
                IsEmpty = false;
            }
        }

        int _Length;
        public int Length
        {
            get => _Length;
            set
            {
                _Length = value;
                IsEmpty = false;
            }
        }

        int _Depth;
        public int Depth
        {
            get => _Depth;
            set
            {
                _Depth = value;
                IsEmpty = false;
            }
        }

        public bool IsEmpty { get; protected set; }

        public static Size3d Empty = new Size3d(0, 0, 0) { IsEmpty = true };

        public Size3d() { }

        public Size3d(int length, int width, int depth)
        {
            Length = length;
            Width = width;
            Depth = depth;
        }

        public static bool Equals(Size3d lhs, Size3d rhs)
        {
            if (lhs is null && rhs is null)
                return true;

            if (lhs is null || rhs is null)
                return false;

            return lhs.Width == rhs.Width && lhs.Length == rhs.Length && lhs.Depth == lhs.Depth;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Size3d;
            if (other is null)
                return false;

            return Equals(this, other);
        }

        public override int GetHashCode() => $"{Width}-{Length}-{Depth}".GetHashCode();

        public static bool operator ==(Size3d lhs, Size3d rhs) => Equals(lhs, rhs);

        public static bool operator !=(Size3d lhs, Size3d rhs) => !Equals(lhs, rhs);
    }
}
