using System.Drawing;

public static class Extensions
{
    public static Point FromDelta(this Point position, int xd, int yd) =>
        new Point(position.X + xd, position.Y + yd);

    public static Size FromDelta(this Size size, int dWidth, int dHeight) =>
        new Size(size.Width + dWidth, size.Height + dHeight);
}
