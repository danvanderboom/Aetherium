using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public static class Extensions
    {
        public static Position[] ToPoints(this Rectangle rectangle) =>
            new Position[]
            {
                new Position(rectangle.X, rectangle.Y, 0),
                new Position(rectangle.X + rectangle.Width, rectangle.Y, 0),
                new Position(rectangle.X, rectangle.Y + rectangle.Height, 0),
                new Position(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, 0),
            };

        public static Rectangle ToEnclosingRectangle(this Rectangle rectangle) =>
            new Rectangle(rectangle.X - 1, rectangle.Y - 1, rectangle.Width + 2, rectangle.Height + 2);

        public static List<SpaceTimeMemory> AtLocation(
            this IDictionary<Position, List<SpaceTimeMemory>> memories, Position location) =>
            memories.ContainsKey(location) ? memories[location] : new List<SpaceTimeMemory>();
    }
}
