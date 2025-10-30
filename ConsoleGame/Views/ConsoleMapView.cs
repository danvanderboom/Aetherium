using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;
using ConsoleGame.Geometry;
using ConsoleGame.Systems;
using ConsoleGame.Lighting;

namespace ConsoleGame.Views
{
    public class ConsoleMapView : ConsoleView
    {
        int symbolWidth = 2;

        public WorldDirection Heading { get; set; } = WorldDirection.North;

        public ConsoleColor[,]? GridColoring { get; set; }

        public World? World { get; set; }

        public WorldLocation? WorldLocation { get; set; }

        public List<TileType> TileTypes { get; set; }

        public VisionFrame? Vision { get; set; }

        public LightFrame? Lighting { get; set; }

        VisionSystem visionSystem = new VisionSystem();
        LightingSystem lightingSystem = new LightingSystem();
        Guid lastMoveTimestamp = Guid.Empty;
        WorldLocation lastOrigin = WorldLocation.None;

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
                    WorldLocation.X - (ContentSize.Width / symbolWidth) / 2,
                    WorldLocation.Y - ContentSize.Height / 2),
                size: new Size(ContentSize.Width / symbolWidth, ContentSize.Height));

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

            // Recompute vision and lighting when player moved or origin changed
            var worldWidth = size.Width / symbolWidth;
            var worldHeight = size.Height;
            var bounds = VisibleWorldRectangle ?? new Rectangle(
                WorldLocation.X - worldWidth / 2, 
                WorldLocation.Y - worldHeight / 2, 
                worldWidth, 
                worldHeight);
            var maxRange = Math.Max(bounds.Width, bounds.Height) / 2 + 1;
            if (Vision == null || Lighting == null || lastMoveTimestamp != World.CharacterMoveTimestamp || lastOrigin != WorldLocation)
            {
                // Compute lighting first
                Lighting = lightingSystem.ComputeLighting(World, bounds, WorldLocation.Z);
                
                // Compute vision with lighting integration
                Vision = visionSystem.ComputeVision(World, WorldLocation, bounds, maxRange, Lighting);
                lastMoveTimestamp = World.CharacterMoveTimestamp;
                lastOrigin = WorldLocation;
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

            // Calculate world coordinate offsets based on content size (world coordinates)
            // Not screen pixel positions - we need half the world width/height
            var xoffset = worldWidth / 2;
            var yoffset = worldHeight / 2;

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

                    // Store unrotated world location for FOV visibility check
                    // FOV is computed in unrotated world coordinates, so we must check visibility
                    // using the unrotated location, not the rotated one
                    var unrotatedLocation = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);

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

                    // Visibility check: skip rendering if not visible
                    // CRITICAL: Use unrotatedLocation for FOV check, not rotated location
                    // FOV is computed in world coordinates, so Vision.Visuals contains unrotated world coordinates
                    if (Vision != null && !Vision.Visuals.ContainsKey(unrotatedLocation))
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                        continue;
                    }

                    // optionally: display grid coloring
                    ConsoleColor? color = null;
                    if (GridColoring != null)
                    {
                        var gridColorHeight = GridColoring.GetLength(0);
                        var gridColorWidth = GridColoring.GetLength(1);

                        color = GridColoring[
                            Math.Abs(location.Y % gridColorHeight), 
                            Math.Abs(location.X % gridColorWidth)];

                        if (location == WorldLocation) // TODO: remove this part
                        {
                            // TODO: move these specific TileType references out of here
                            DrawTileType(World.TileTypes["Player"], ConsoleColor.Magenta);
                            continue;
                        }
                        else if (World.EntitiesByLocation.ContainsKey(location))
                        {
                            DrawTileType(World.TileTypes["None"], color);
                            continue;
                        }
                    }

                    // Get light level for this location (using unrotated location for lighting)
                    var lightLevel = Lighting?.GetLightLevel(unrotatedLocation) ?? 0.0;

                    if (location == WorldLocation) // TODO: remove this part
                    {
                        DrawTileType(World.TileTypes["Player"], color, lightLevel);
                    }
                    else if (World.EntitiesByLocation.TryGetValue(location, out var entities))
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
                                DrawTile(tile, color, lightLevel);
                        }
                        else if (gameObjects.Count > 0) // show game objects on top of terrain
                        {
                            var first = gameObjects.First();

                            var tile = first.Get<Tile>();
                            DrawTile(tile, color, lightLevel);
                        }
                        else if (terrainEntities.Count > 0) // show terrain below everything
                        {
                            DrawTile(terrainEntities.First().Get<Tile>(), color, lightLevel);
                        }
                    }
                    else
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                    }
                }
            }

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

        public void DrawTile(Tile tile, ConsoleColor? gridColor = null, double lightLevel = 1.0) => 
            DrawTileType(tile.Type, gridColor, lightLevel);

        public void DrawTileType(TileType tileType, ConsoleColor? gridColor = null, double lightLevel = 1.0)
        {
            // Handle missing settings gracefully
            var bgColor = gridColor.HasValue ? gridColor.Value 
                : (tileType.Settings.TryGetValue("BackgroundColor", out var bg) 
                    ? Enum.Parse<ConsoleColor>(bg) 
                    : ConsoleColor.Black);

            var fgColor = tileType.Settings.TryGetValue("ForegroundColor", out var fg)
                ? Enum.Parse<ConsoleColor>(fg)
                : ConsoleColor.White;

            var mapChar = tileType.Settings.TryGetValue("MapCharacter", out var ch)
                ? ch
                : "?";

            // Apply lighting dimming: interpolate between black (no light) and original color (full light)
            bgColor = DimColor(bgColor, lightLevel);
            fgColor = DimColor(fgColor, lightLevel);

            Console.BackgroundColor = bgColor;
            Console.ForegroundColor = fgColor;

            for (int i = 0; i < symbolWidth; i++)
                Console.Write(mapChar);
        }

        /// <summary>
        /// Dims a console color based on light level.
        /// Returns a darker shade interpolated toward black when light is low.
        /// </summary>
        private ConsoleColor DimColor(ConsoleColor originalColor, double lightLevel)
        {
            if (lightLevel >= 1.0)
                return originalColor;
            if (lightLevel <= 0.0)
                return ConsoleColor.Black;

            // For simplicity, map to a darker shade in the palette
            // We use a simple interpolation: choose between original color and black
            // Based on available console colors, we'll use DarkGray as intermediate
            
            // Map light levels:
            // 0.0 - 0.3: Black
            // 0.3 - 0.6: DarkGray
            // 0.6 - 0.8: Gray
            // 0.8 - 1.0: Original color

            if (lightLevel < 0.3)
                return ConsoleColor.Black;
            else if (lightLevel < 0.6)
                return ConsoleColor.DarkGray;
            else if (lightLevel < 0.8)
            {
                // Use gray or a slightly brighter version
                if (originalColor == ConsoleColor.Black)
                    return ConsoleColor.DarkGray;
                if (originalColor == ConsoleColor.DarkGray)
                    return ConsoleColor.Gray;
                // For other colors, try to get a darker variant if available
                return GetDarkerVariant(originalColor) ?? ConsoleColor.Gray;
            }
            else
            {
                // Between 0.8 and 1.0, blend original with gray
                return GetSlightlyDarkerVariant(originalColor) ?? originalColor;
            }
        }

        private ConsoleColor? GetDarkerVariant(ConsoleColor color)
        {
            // Map common colors to darker variants
            return color switch
            {
                ConsoleColor.Red => ConsoleColor.DarkRed,
                ConsoleColor.Green => ConsoleColor.DarkGreen,
                ConsoleColor.Blue => ConsoleColor.DarkBlue,
                ConsoleColor.Yellow => ConsoleColor.DarkYellow,
                ConsoleColor.Cyan => ConsoleColor.DarkCyan,
                ConsoleColor.Magenta => ConsoleColor.DarkMagenta,
                ConsoleColor.White => ConsoleColor.Gray,
                ConsoleColor.Gray => ConsoleColor.DarkGray,
                _ => null
            };
        }

        private ConsoleColor? GetSlightlyDarkerVariant(ConsoleColor color)
        {
            // For high light levels, use slightly darker version
            return color switch
            {
                ConsoleColor.White => ConsoleColor.Gray,
                ConsoleColor.Gray => ConsoleColor.DarkGray,
                _ => null
            };
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
                RelativeDirection.Forward => 0,
                RelativeDirection.Left => 1,
                RelativeDirection.Backward => 2,
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
