using System;

namespace ConsoleGameModel
{
    public class RectangleDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public RectangleDto()
        {
        }

        public RectangleDto(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"Rect({X}, {Y}, {Width}, {Height})";
    }
}

