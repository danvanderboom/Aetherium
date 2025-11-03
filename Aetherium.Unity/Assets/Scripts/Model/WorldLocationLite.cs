using System;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Unity-friendly representation of WorldLocationDto.
    /// </summary>
    [Serializable]
    public class WorldLocationLite
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public WorldLocationLite()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }

        public WorldLocationLite(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

