using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;
using ConsoleGame.Geometry;

namespace ConsoleGame.Views
{
    public class ConsoleMapView : ConsoleView
    {
        int symbolWidth = 2;

        public WorldDirection Heading { get; set; } = WorldDirection.North;

        public ConsoleColor[,]? GridColoring { get; set; }

        World? _World;
        public World? World
        {
            get => _World;
            set
            {
                if (value != _World)
                {
                    _World = value;

                    DrawContents();
                }
            }
        }

        WorldLocation? _WorldLocation;
        public WorldLocation? WorldLocation
        {
            get => _WorldLocation;
            set
            {
                if (value != _WorldLocation)
                {
                    _WorldLocation = value;
                    DrawContents();
                }
            }
        }

        public List<TileType> TileTypes { get; set; }

        public Point ContentScreenPosition =>
            HasFrame ? ScreenPosition.FromDelta(+1, +1) : ScreenPosition;

        public Size ContentSize =>
            HasFrame ? Size.FromDelta(-2, -2) : Size;

        public TileType GetTileType(string name) => 
            TileTypes.First(t => t.Name == name);

        public ConsoleMapView() : base() 
        {
            TileTypes = new List<TileType>();
        }

        public ConsoleMapView(Point screenPosition, Size size, bool hasFrame = true) 
            : base(screenPosition, size, hasFrame)
        {
            TileTypes = new List<TileType>();
        }

        public Point CenterScreenPosition => new Point(
            ScreenPosition.X + (Size.Width + 1) / 2,
            ScreenPosition.Y + (Size.Height + 1) / 2);

        public Rectangle? VisibleWorldRectangle => WorldLocation is null ? (Rectangle?)null :
            new Rectangle(
                location: new Point(
                    WorldLocation.X - (CenterScreenPosition.X - ScreenPosition.X),
                    WorldLocation.Y - (CenterScreenPosition.Y - ScreenPosition.Y)),
                size: ContentSize);

        protected override void DrawContents(Point screenPosition, Size size)
        {
            if (World == null || WorldLocation == null)
            {
                Console.BackgroundColor = BackgroundColor;

                var hline = new string(' ', size.Width);

                for (int y = screenPosition.Y; y < screenPosition.Y + size.Height; y++)
                {
                    Console.SetCursorPosition(screenPosition.X, y);
                    Console.Write(hline);
                }

                return;
            }

            var rotationDegees = Heading switch
            {
                WorldDirection.North => 0,
                WorldDirection.West => 90,
                WorldDirection.South => 180,
                WorldDirection.East => 270,
                _ => throw new ArgumentException("Invalid heading detected")
            };

            var centerScreenPosition = new Point(
                screenPosition.X + (size.Width + 1) / 2,
                screenPosition.Y + (size.Height + 1) / 2);

            var xoffset = (centerScreenPosition.X - screenPosition.X) / symbolWidth - 1;
            var yoffset = centerScreenPosition.Y - screenPosition.Y - 1;

            var center = WorldLocation.AsVector3();

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width / symbolWidth; x++)
                {
                    Console.SetCursorPosition(screenPosition.X + (x * symbolWidth), screenPosition.Y + y);

                    var vLocation = new Vector3(
                        WorldLocation.X + x - xoffset,
                        WorldLocation.Y + y - yoffset,
                        WorldLocation.Z);

                    // rotate worldLocation around Z axis based on Heading property counterclockwise
                    // North = 0 degrees, West = 90, South = 180, East = 270
                    // 1. translate location by -center.X,-center.Y (so center is at 0,0)
                    // 2. rotate location around center point
                    // 3. reverse the translation in step 1

                    if (Heading != WorldDirection.North)
                    {
                        vLocation = vLocation.Translate(-center.X, -center.Y, 0);

                        if (Heading == WorldDirection.South)
                            vLocation = vLocation.Rotate180();
                        else if (Heading == WorldDirection.West)
                            vLocation = vLocation.Rotate90CW();
                        else if (Heading == WorldDirection.East)
                            vLocation = vLocation.Rotate90CCW();

                        vLocation = vLocation.Translate(center.X, center.Y, 0);
                    }

                    var location = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);

                    // optionally: display grid coloring
                    ConsoleColor? color = null;
                    if (GridColoring != null)
                    {
                        var gridColorHeight = GridColoring.GetUpperBound(0) + 1;
                        var gridColorWidth = GridColoring.GetUpperBound(1) + 1;

                        color = GridColoring[
                            Math.Abs(location.Y % gridColorHeight), 
                            Math.Abs(location.X % gridColorWidth)];
                    }

                    if (World.EntitiesByLocation.TryGetValue(location, out var entities))
                    {
                        var characterEntities = entities.Values.OfType<Character>().ToList<Entity>();
                        var terrainEntities = entities.Values.OfType<Terrain>().ToList<Entity>();

                        var gameObjects = entities.Values
                            .Where(e => !characterEntities.Contains(e) && !terrainEntities.Contains(e))
                            .ToList();

                        if (characterEntities.Count > 0) // show characters on top
                        {
                            var first = characterEntities.First();

                            var health = first.Get<Health>();
                            var alive = health?.Level > 0;
                            
                            var tile = first.Get<Tile>();
                            if (tile != null)
                                DrawTile(tile, color);
                        }
                        else if (gameObjects.Count > 0) // show game objects on top of terrain
                        {
                            var first = gameObjects.First();

                            var tile = first.Get<Tile>();
                            DrawTile(tile, color);
                        }
                        else if (terrainEntities.Count > 0) // show terrain below everything
                        {
                            DrawTile(terrainEntities.First().Get<Tile>(), color);
                        }
                    }
                    else
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                    }
                }
            }

            // TODO: move to separate view

            //Console.SetCursorPosition(
            //    mapLocation.X - 1, // start at the map frame
            //    mapLocation.Y + size.Height + 2); // map frame + blank line

            //Console.BackgroundColor = backgroundColor;
            //Console.ForegroundColor = ConsoleColor.Cyan;
            //Console.Write(
            //    CenterText($"{location.X}, {location.Y}, {location.FromDelta(0, 0, -gameWorldSize.Depth + 1).Z}",
            //    size.Width + 2));

            Console.BackgroundColor = BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            //

            Console.SetCursorPosition(
                ScreenPosition.X,
                ScreenPosition.Y + size.Height + 2); // skip line

            Console.Write(
                CenterText($"Visible World Rectangle: {VisibleWorldRectangle?.X}, {VisibleWorldRectangle?.Y}, {VisibleWorldRectangle?.Width}, {VisibleWorldRectangle?.Height}",
                Size.Width));

            //

            Console.SetCursorPosition(
                ScreenPosition.X,
                ScreenPosition.Y + size.Height + 3); // skip line

            Console.Write(
                CenterText($"World Location: {WorldLocation.X}, {WorldLocation.Y}, {WorldLocation.Z}",
                Size.Width));
        }

        public void DrawTile(Tile tile, ConsoleColor? gridColor = null) => 
            DrawTileType(tile.Type, gridColor);

        public void DrawTileType(TileType tileType, ConsoleColor? gridColor = null)
        {
            Console.BackgroundColor = gridColor.HasValue ? gridColor.Value 
                : Enum.Parse<ConsoleColor>(tileType.Settings["BackgroundColor"]);

            Console.ForegroundColor = Enum.Parse<ConsoleColor>(tileType.Settings["ForegroundColor"]);

            for (int i = 0; i < symbolWidth; i++)
                Console.Write(tileType.Settings["MapCharacter"]);
        }

        public void RotateRight()
        {
            Heading = Heading switch
            {
                WorldDirection.North => WorldDirection.East,
                WorldDirection.East => WorldDirection.South,
                WorldDirection.South => WorldDirection.West,
                WorldDirection.West => WorldDirection.North,
                _ => throw new ArgumentException("Heading is invalid")
            };
        }

        public void RotateLeft()
        {
            Heading = Heading switch
            {
                WorldDirection.North => WorldDirection.West,
                WorldDirection.West => WorldDirection.South,
                WorldDirection.South => WorldDirection.East,
                WorldDirection.East => WorldDirection.North,
                _ => throw new ArgumentException("Heading is invalid")
            };
        }

        public void Move(RelativeDirection direction, int count = 1)
        {
            if (WorldLocation == null)
                return;

            var rightAngleRotationsCounterclockwise = direction switch
            {
                RelativeDirection.Up => 0,
                RelativeDirection.Left => 1,
                RelativeDirection.Down => 2,
                RelativeDirection.Right => 3,
                _ => throw new InvalidOperationException("Invalid RelativeDirection")
            };

            var bearing = Heading;
            for (int i = 0; i < rightAngleRotationsCounterclockwise; i++)
                bearing = bearing.RotateLeft();

            WorldLocation = bearing switch
            {
                WorldDirection.North => WorldLocation.FromDelta(0, -count, 0),
                WorldDirection.East => WorldLocation.FromDelta(count, 0, 0),
                WorldDirection.South => WorldLocation.FromDelta(0, count, 0),
                WorldDirection.West => WorldLocation.FromDelta(-count, 0, 0),
                _ => throw new ArgumentException("Heading is invalid")
            };
        }
    }
}
