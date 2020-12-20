using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ConsoleGame.Components;
using ConsoleGame.Geometry;
using ConsoleGame;

public static class Extensions
{
    public static RelativeDirection RotateRight(this RelativeDirection dir) => dir switch
    {
        RelativeDirection.Up => RelativeDirection.Right,
        RelativeDirection.Right => RelativeDirection.Down,
        RelativeDirection.Down => RelativeDirection.Left,
        RelativeDirection.Left => RelativeDirection.Up,
        _ => throw new InvalidOperationException("Invalid RelativeDirection")
    };

    public static RelativeDirection RotateLeft(this RelativeDirection dir) => dir switch
    {
        RelativeDirection.Up => RelativeDirection.Left,
        RelativeDirection.Left => RelativeDirection.Down,
        RelativeDirection.Down => RelativeDirection.Right,
        RelativeDirection.Right => RelativeDirection.Up,
        _ => throw new InvalidOperationException("Invalid RelativeDirection")
    };

    public static WorldDirection RotateRight(this WorldDirection dir) => dir switch
    {
        WorldDirection.North => WorldDirection.East,
        WorldDirection.East => WorldDirection.South,
        WorldDirection.South => WorldDirection.West,
        WorldDirection.West => WorldDirection.North,
        _ => throw new InvalidOperationException("Invalid CardinalDirection")
    };

    public static WorldDirection RotateLeft(this WorldDirection dir) => dir switch
    {
        WorldDirection.North => WorldDirection.West,
        WorldDirection.West => WorldDirection.South,
        WorldDirection.South => WorldDirection.East,
        WorldDirection.East => WorldDirection.North,
        _ => throw new InvalidOperationException("Invalid CardinalDirection")
    };

    public static Vector3 AsVector3(this WorldLocation location) =>
        new Vector3(location.X, location.Y, location.Z);

    public static WorldLocation[] ToPoints(this Rectangle rectangle) =>
        new WorldLocation[]
        {
            new WorldLocation(rectangle.X, rectangle.Y, 0),
            new WorldLocation(rectangle.X + rectangle.Width, rectangle.Y, 0),
            new WorldLocation(rectangle.X, rectangle.Y + rectangle.Height, 0),
            new WorldLocation(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, 0),
        };

    public static Rectangle ToEnclosingRectangle(this Rectangle rectangle) =>
        new Rectangle(rectangle.X - 1, rectangle.Y - 1, rectangle.Width + 2, rectangle.Height + 2);

    public static List<SpaceTimeMemory> AtLocation(
        this IDictionary<WorldLocation, List<SpaceTimeMemory>> memories, WorldLocation location) =>
        memories.ContainsKey(location) ? memories[location] : new List<SpaceTimeMemory>();

    public static WorldLocation FromDelta(this WorldLocation position, int xd, int yd, int zd) =>
        new WorldLocation(position.X + xd, position.Y + yd, position.Z + zd);

    public static Point FromDelta(this Point position, int xd, int yd) =>
        new Point(position.X + xd, position.Y + yd);

    public static Size FromDelta(this Size size, int dWidth, int dHeight) =>
        new Size(size.Width + dWidth, size.Height + dHeight);

    public static int ForceInRange(this int value, int min, int max) => 
        Math.Min(max, Math.Max(min, value));

    //public static TileType ToTileType(this TerrainType terrainType) =>
    //    (TileType)Enum.Parse(typeof(TileType), terrainType.ToString());
}
