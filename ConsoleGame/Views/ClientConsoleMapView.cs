using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGameModel;
using ConsoleGame.Client;

namespace ConsoleGame.Views
{
    public class ClientConsoleMapView : ConsoleView
    {
        int symbolWidth = 2;

        public ConsoleGame.WorldDirection Heading { get; set; } = ConsoleGame.WorldDirection.North;
        public ConsoleColor[,]? GridColoring { get; set; }
        public WorldLocationDto? WorldLocation { get; set; }
        public PerceptionDto? Perception { get; set; }

        public Point ContentScreenPosition =>
            HasFrame ? ScreenPosition.FromDelta(+1, +1) : ScreenPosition;

        public Size ContentSize =>
            HasFrame ? Size.FromDelta(-2, -2) : Size;

        public ClientConsoleMapView() : base()
        {
        }

        public ClientConsoleMapView(Point screenPosition, Size size, bool hasFrame = true)
            : base(screenPosition, size, hasFrame)
        {
        }

        public Point CenterScreenPosition => new Point(
            ScreenPosition.X + (Size.Width + 1) / 2,
            ScreenPosition.Y + (Size.Height + 1) / 2);

        // Note: Visible bounds are now relative to player (who is always at 0,0,0)
        public Rectangle? VisibleWorldRectangle => WorldLocation is null ? (Rectangle?)null :
            new Rectangle(
                location: new Point(
                    -(ContentSize.Width / symbolWidth) / 2,
                    -ContentSize.Height / 2),
                size: new Size(ContentSize.Width / symbolWidth, ContentSize.Height));

        protected override void DrawContents(Point screenPosition, Size size)
        {
            if (Perception == null || WorldLocation == null)
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

            // Update local heading from perception
            // Note: PlayerLocation is always (0,0,0) - we use relative coordinates only
            Heading = Perception.PlayerHeading.ToClientDirection();
            WorldLocation = Perception.PlayerLocation; // This is always (0,0,0) for relative coordinates

            var worldWidth = size.Width / symbolWidth;
            var worldHeight = size.Height;

            var centerScreenPosition = new Point(
                screenPosition.X + (size.Width + 1) / 2,
                screenPosition.Y + (size.Height + 1) / 2);

            // Player is always at center (0,0,0) in relative coordinates
            var xoffset = worldWidth / 2;
            var yoffset = worldHeight / 2;

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width / symbolWidth; x++)
                {
                    Console.SetCursorPosition(screenPosition.X + (x * symbolWidth), screenPosition.Y + y);

                    // Calculate relative coordinates (offsets from player at center)
                    var relativeX = x - xoffset;
                    var relativeY = y - yoffset;
                    var relativeZ = 0; // Player is always on Z=0 relative to themselves

                    // Create location key for looking up in perception visuals (using relative coordinates)
                    var locationKey = $"{relativeX},{relativeY},{relativeZ}";

                    // Check if this location is visible
                    if (!Perception.Visuals.TryGetValue(locationKey, out var visual))
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                        continue;
                    }

                    // Optionally: display grid coloring
                    ConsoleColor? color = null;
                    if (GridColoring != null)
                    {
                        var gridColorHeight = GridColoring.GetLength(0);
                        var gridColorWidth = GridColoring.GetLength(1);

                        // Use relative coordinates for grid coloring
                        color = GridColoring[
                            Math.Abs(relativeY % gridColorHeight),
                            Math.Abs(relativeX % gridColorWidth)];
                    }

                    // Check if this is the player location (always at 0,0,0 in relative coordinates)
                    if (relativeX == 0 && relativeY == 0 && relativeZ == 0)
                    {
                        // Draw player
                        if (Perception.TileTypes.TryGetValue("Player", out var playerTileType))
                        {
                            DrawTileType(playerTileType, color, visual.LightLevel);
                        }
                        else
                        {
                            // Fallback player rendering
                            DrawTileType(new TileTypeDto 
                            { 
                                Name = "Player",
                                Settings = new Dictionary<string, string>
                                {
                                    ["MapCharacter"] = "@",
                                    ["ForegroundColor"] = "Yellow",
                                    ["BackgroundColor"] = "Black"
                                }
                            }, color, visual.LightLevel);
                        }
                    }
                    else if (visual.Terrain != null)
                    {
                        // Draw terrain
                        DrawTileType(visual.Terrain, color, visual.LightLevel);
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

            // Display location info
            Console.SetCursorPosition(
                ScreenPosition.X,
                ScreenPosition.Y + size.Height + 2);

            // Note: We no longer display absolute world coordinates (client should not know them)
            // Visible bounds are still shown but they're relative to player position
            Console.Write(
                CenterText($"Visible Bounds: {Perception.VisibleBounds.X}, {Perception.VisibleBounds.Y}, {Perception.VisibleBounds.Width}, {Perception.VisibleBounds.Height}",
                Size.Width));

            Console.SetCursorPosition(
                ScreenPosition.X,
                ScreenPosition.Y + size.Height + 3);

            Console.Write(
                CenterText($"Player Position: (0, 0, 0) [relative coordinates only]",
                Size.Width));
        }

        public void DrawTileType(TileTypeDto tileType, ConsoleColor? gridColor = null, double lightLevel = 1.0)
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

            // Apply lighting dimming
            bgColor = DimColor(bgColor, lightLevel);
            fgColor = DimColor(fgColor, lightLevel);

            Console.BackgroundColor = bgColor;
            Console.ForegroundColor = fgColor;

            for (int i = 0; i < symbolWidth; i++)
                Console.Write(mapChar);
        }

        private ConsoleColor DimColor(ConsoleColor originalColor, double lightLevel)
        {
            if (lightLevel >= 1.0)
                return originalColor;
            if (lightLevel <= 0.0)
                return ConsoleColor.Black;

            if (lightLevel < 0.3)
                return ConsoleColor.Black;
            else if (lightLevel < 0.6)
                return ConsoleColor.DarkGray;
            else if (lightLevel < 0.8)
            {
                if (originalColor == ConsoleColor.Black)
                    return ConsoleColor.DarkGray;
                if (originalColor == ConsoleColor.DarkGray)
                    return ConsoleColor.Gray;
                return GetDarkerVariant(originalColor) ?? ConsoleColor.Gray;
            }
            else
            {
                return GetSlightlyDarkerVariant(originalColor) ?? originalColor;
            }
        }

        private ConsoleColor? GetDarkerVariant(ConsoleColor color)
        {
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
            return color switch
            {
                ConsoleColor.White => ConsoleColor.Gray,
                ConsoleColor.Gray => ConsoleColor.DarkGray,
                _ => null
            };
        }
    }
}

