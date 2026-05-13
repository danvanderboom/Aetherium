#nullable enable
using System;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of RectangleDto.
    /// </summary>
    [Serializable]
    public class RectangleLite
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public RectangleLite()
        {
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
        }

        public RectangleLite(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"Rect({X}, {Y}, {Width}, {Height})";
    }
}

