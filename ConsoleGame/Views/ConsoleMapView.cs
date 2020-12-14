using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;

namespace ConsoleGame.Views
{
    public class ConsoleMapView : ConsoleView
    {
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

            var centerScreenPosition = new Point(
                screenPosition.X + (size.Width + 1) / 2,
                screenPosition.Y + (size.Height + 1) / 2);

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    Console.SetCursorPosition(screenPosition.X + x, screenPosition.Y + y);

                    var xoffset = centerScreenPosition.X - screenPosition.X - 1;
                    var yoffset = centerScreenPosition.Y - screenPosition.Y - 1;

                    var worldLocation = new WorldLocation(
                        WorldLocation.X + x - xoffset,
                        WorldLocation.Y + y - yoffset,
                        WorldLocation.Z);

                    if (World.EntitiesByLocation.TryGetValue(worldLocation, out var entities))
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
                                DrawTile(tile);
                        }
                        else if (gameObjects.Count > 0) // show game objects on top of terrain
                        {
                            var first = gameObjects.First();

                            var tile = first.Get<Tile>();
                            DrawTile(tile);
                        }
                        else if (terrainEntities.Count > 0) // show terrain below everything
                        {
                            DrawTile(terrainEntities.First().Get<Tile>());
                        }
                    }
                    else
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(' ');
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

        public void DrawTileType(TileType tileType)
        {
            Console.BackgroundColor = Enum.Parse<ConsoleColor>(tileType.Settings["BackgroundColor"]);
            Console.ForegroundColor = Enum.Parse<ConsoleColor>(tileType.Settings["ForegroundColor"]);

            Console.Write(tileType.Settings["MapCharacter"]);
        }

        public void DrawTile(Tile tile)
        {
            Console.BackgroundColor = Enum.Parse<ConsoleColor>(tile.Type.Settings["BackgroundColor"]);
            Console.ForegroundColor = Enum.Parse<ConsoleColor>(tile.Type.Settings["ForegroundColor"]);

            Console.Write(tile.Type.Settings["MapCharacter"]);
        }
    }
}
