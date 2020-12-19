namespace ConsoleGame.Geometry
{
    public struct LineSegment3
    {
        public Vector3 Start { get; set; }
        public Vector3 End { get; set; }

        public Vector3 Vector => new Vector3(End.X - Start.X, End.Y - Start.Y, End.Z - Start.Z);

        public LineSegment3(Vector3 start, Vector3 end)
        {
            Start = start;
            End = end;
        }
    }
}
