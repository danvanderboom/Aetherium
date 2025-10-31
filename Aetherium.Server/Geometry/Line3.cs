namespace Aetherium.Geometry
{
    public struct Line3
    {
        public Vector3 Point { get; set; }
        public Vector3 Direction { get; set; }

        public Line3(Vector3 point, Vector3 direction)
        {
            Point = point;
            Direction = direction;
        }
    }
}

