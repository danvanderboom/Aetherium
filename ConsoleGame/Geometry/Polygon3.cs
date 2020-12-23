using System.Collections.Generic;

namespace ConsoleGame.Geometry
{
    public struct Polygon3
    {
        public List<Vector3> Points { get; set; }

        public Polygon3(IEnumerable<Vector3> points)
        {
            Points = new List<Vector3>(points);
        }
    }
}
