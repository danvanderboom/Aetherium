using System;
using System.Linq;

namespace ConsoleGame.Geometry
{
    public struct Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        public bool IsEmpty { get; set; }

        public static Vector3 Empty => new Vector3(isEmpty: true);

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
            IsEmpty = false;
        }

        public Vector3(bool isEmpty)
        {
            X = 0;
            Y = 0;
            Z = 0;
            IsEmpty = isEmpty;
        }

        public Vector3 Normalize() =>
            new Vector3(X / Magnitude, Y / Magnitude, Z / Magnitude);

        public Vector3 Scale(double factor) =>
            new Vector3(X * factor, Y * factor, Z * factor);

        public Vector3 Translate(double x, double y, double z) =>
            new Vector3(X + x, Y + y, Z + z);

        public Vector3 Add(Vector3 other) =>
            new Vector3(X + other.X, Y + other.Y, Z + other.Z);

        public Vector3 Subtract(Vector3 other) =>
            new Vector3(X - other.X, Y - other.Y, Z - other.Z);

        public double Dot(Vector3 other) =>
            X * other.X + Y * other.Y + Z * other.Z;
    }
}
