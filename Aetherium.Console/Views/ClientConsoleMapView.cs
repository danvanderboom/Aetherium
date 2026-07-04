using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Aetherium.Model;
using Aetherium.Client;
using Aetherium.Monitoring;

namespace Aetherium.Views
{
    public class ClientConsoleMapView : ConsoleView
    {
        int symbolWidth = 2;

        public Aetherium.WorldDirection Heading { get; set; } = Aetherium.WorldDirection.North;
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

            // Diagnostic tracking for test mode (check if .ui-test directory exists in project root)
            var currentDir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            var projectRoot = currentDir;
            // If we're in Aetherium subdirectory, go up one level
            if (currentDir.Name == "Aetherium" && currentDir.Parent != null)
                projectRoot = currentDir.Parent;
            var uiTestDir = System.IO.Path.Combine(projectRoot.FullName, ".ui-test");
            var testMode = System.IO.Directory.Exists(uiTestDir) || Environment.GetEnvironmentVariable("UI_SELFTEST_MODE") == "1";
            var keysLookedUp = new System.Collections.Generic.HashSet<string>();
            var keysFound = new System.Collections.Generic.HashSet<string>();
            var keysNotFound = new System.Collections.Generic.HashSet<string>();

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
                    
                    if (testMode)
                        keysLookedUp.Add(locationKey);

                    // Check if this location is visible
                    if (!Perception.Visuals.TryGetValue(locationKey, out var visual))
                    {
                        if (testMode)
                            keysNotFound.Add(locationKey);
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                        continue;
                    }
                    
                    if (testMode)
                        keysFound.Add(locationKey);

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
                    else
                    {
                        // Characters (monsters/NPCs, other players) take priority
                        // over items and terrain — a monster standing on treasure is
                        // the thing you need to see.
                        var characterAtLocation = Perception.VisibleCharacters?.FirstOrDefault(
                            c => c.Location != null &&
                            c.Location.X == relativeX &&
                            c.Location.Y == relativeY &&
                            c.Location.Z == relativeZ);

                        // Check for items at this location next
                        var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                            item => item.Location != null &&
                            item.Location.X == relativeX &&
                            item.Location.Y == relativeY &&
                            item.Location.Z == relativeZ);

                        switch (ResolveContentLayer(
                            characterAtLocation != null, itemAtLocation != null, visual.Terrain != null))
                        {
                            case MapCellLayer.Character:
                                DrawCharacter(characterAtLocation!, color, visual.LightLevel);
                                break;
                            case MapCellLayer.Item:
                                DrawItem(itemAtLocation!, color, visual.LightLevel);
                                break;
                            case MapCellLayer.Terrain:
                                DrawTileType(visual.Terrain!, color, visual.LightLevel);
                                break;
                            default:
                                Console.BackgroundColor = BackgroundColor;
                                Console.Write(new string(' ', symbolWidth));
                                break;
                        }
                    }
                }
            }
            
            // Diagnostic output (save to file if in test mode)
            if (testMode && keysLookedUp.Count > 0) // Only log if we tracked anything
            {
                var diagPath = System.IO.Path.Combine(uiTestDir, "render_diagnostics.txt");
                try
                {
                    System.IO.Directory.CreateDirectory(uiTestDir);
                    var diagContent = $"Keys in Perception.Visuals: {string.Join(", ", Perception.Visuals.Keys.Take(10))}\n" +
                        $"Keys looked up (sample): {string.Join(", ", keysLookedUp.Take(10))}\n" +
                        $"Keys found: {keysFound.Count}, Keys not found: {keysNotFound.Count}\n" +
                        $"Sample not found keys: {string.Join(", ", keysNotFound.Take(10))}\n" +
                        $"worldWidth={worldWidth}, worldHeight={worldHeight}, xoffset={xoffset}, yoffset={yoffset}\n" +
                        $"size.Width={size.Width}, size.Height={size.Height}, symbolWidth={symbolWidth}\n";
                    System.IO.File.WriteAllText(diagPath, diagContent);
                }
                catch { /* ignore write errors */ }
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

            // Inventory summary (simple one-line list)
            Console.SetCursorPosition(
                ScreenPosition.X,
                ScreenPosition.Y + size.Height + 4);

            if (Perception.Inventory != null && Perception.Inventory.Items.Any())
            {
                var items = string.Join(
                    ", ",
                    Perception.Inventory.Items.Select(i => string.IsNullOrEmpty(i.KeyId) ? i.Label : $"{i.Label}({i.KeyId})"));
                Console.Write(CenterText($"Inventory [{Perception.Inventory.Items.Count}/{Perception.Inventory.Capacity}]: {items}", Size.Width));
            }
            else
            {
                Console.Write(CenterText($"Inventory [0/{Perception.Inventory?.Capacity ?? 10}]: (empty)", Size.Width));
            }
        }

        private void DrawItem(ItemDto item, ConsoleColor? gridColor = null, double lightLevel = 1.0)
        {
            // Map key ID to color for keys
            ConsoleColor fgColor = ConsoleColor.White;
            if (!string.IsNullOrEmpty(item.KeyId))
            {
                fgColor = item.KeyId.ToLowerInvariant() switch
                {
                    "red" => ConsoleColor.Red,
                    "blue" => ConsoleColor.Blue,
                    "green" => ConsoleColor.Green,
                    "yellow" => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                };
            }

            var bgColor = gridColor ?? ConsoleColor.Black;
            var icon = item.Icon;
            if (string.IsNullOrEmpty(icon))
                icon = "?";
            if (icon.Length > symbolWidth)
                icon = icon.Substring(0, symbolWidth);

            // Apply lighting/heat dimming based on vision mode
            if (Perception?.CurrentVisionMode == VisionMode.Infrared)
            {
                // Infrared: use heat-based colors
                bgColor = GetInfraredColor(lightLevel, true); // background
                fgColor = GetInfraredColor(lightLevel, false); // foreground
            }
            else
            {
                // Normal vision: dim by light level and apply ambient tint
                bgColor = DimColor(bgColor, lightLevel);
                fgColor = DimColor(fgColor, lightLevel);
                
                // Apply sunrise/sunset tint if in sunlight mode
                if (Perception?.CurrentLightingMode == LightingMode.Sunlight)
                {
                    bgColor = ApplyAmbientTint(bgColor, Perception.AmbientTint);
                    fgColor = ApplyAmbientTint(fgColor, Perception.AmbientTint);
                }
            }

            Console.BackgroundColor = bgColor;
            Console.ForegroundColor = fgColor;

            Console.Write(icon.PadRight(symbolWidth));
        }

        /// <summary>
        /// The content layer drawn at a non-player cell, in priority order.
        /// </summary>
        public enum MapCellLayer { Empty, Character, Item, Terrain }

        /// <summary>
        /// Decides which content layer wins at a (non-player) cell, given what is
        /// present. Characters (monsters/NPCs, other players) draw over items —
        /// a monster standing on treasure is the thing you need to see — which in
        /// turn draw over terrain. Pure so the priority can be unit-tested without a
        /// live console; <see cref="DrawContents"/> renders the chosen layer.
        /// </summary>
        public static MapCellLayer ResolveContentLayer(bool hasCharacter, bool hasItem, bool hasTerrain)
        {
            if (hasCharacter) return MapCellLayer.Character;
            if (hasItem) return MapCellLayer.Item;
            if (hasTerrain) return MapCellLayer.Terrain;
            return MapCellLayer.Empty;
        }

        private void DrawCharacter(CharacterDto character, ConsoleColor? gridColor = null, double lightLevel = 1.0)
        {
            // Characters carry a TileType (glyph + colors) exactly like terrain, so
            // reuse the shared tile renderer — lighting, infrared, and ambient tint
            // then apply uniformly. Fall back to a neutral 'M' marker if the entity
            // reached us without a tile (a bare character).
            var tile = character.Tile ?? new TileTypeDto
            {
                Name = string.IsNullOrEmpty(character.Name) ? "Character" : character.Name,
                Settings = new Dictionary<string, string>
                {
                    ["MapCharacter"] = "M",
                    ["ForegroundColor"] = "DarkRed",
                    ["BackgroundColor"] = "Black"
                }
            };

            DrawTileType(tile, gridColor, lightLevel);
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

            // Apply lighting/heat dimming based on vision mode
            if (Perception?.CurrentVisionMode == VisionMode.Infrared)
            {
                // Infrared: use heat-based colors
                bgColor = GetInfraredColor(lightLevel, true); // background
                fgColor = GetInfraredColor(lightLevel, false); // foreground
            }
            else
            {
                // Normal vision: dim by light level and apply ambient tint
                bgColor = DimColor(bgColor, lightLevel);
                fgColor = DimColor(fgColor, lightLevel);
                
                // Apply sunrise/sunset tint if in sunlight mode
                if (Perception?.CurrentLightingMode == LightingMode.Sunlight)
                {
                    bgColor = ApplyAmbientTint(bgColor, Perception.AmbientTint);
                    fgColor = ApplyAmbientTint(fgColor, Perception.AmbientTint);
                }
            }

            Console.BackgroundColor = bgColor;
            Console.ForegroundColor = fgColor;

            for (int i = 0; i < symbolWidth; i++)
                Console.Write(mapChar);
        }

        /// <summary>
        /// Gets the infrared color for a heat level (0.0-1.0).
        /// Maps heat intensity to color: Black -> DarkRed -> Red -> DarkYellow -> Yellow -> White
        /// </summary>
        private ConsoleColor GetInfraredColor(double heatLevel, bool isBackground)
        {
            if (heatLevel <= 0.05)
                return ConsoleColor.Black;
            
            if (heatLevel < 0.15)
                return ConsoleColor.DarkRed;
            
            if (heatLevel < 0.35)
                return isBackground ? ConsoleColor.DarkRed : ConsoleColor.Red;
            
            if (heatLevel < 0.55)
                return ConsoleColor.Red;
            
            if (heatLevel < 0.75)
                return isBackground ? ConsoleColor.DarkRed : ConsoleColor.DarkYellow;
            
            if (heatLevel < 0.90)
                return ConsoleColor.Yellow;
            
            return ConsoleColor.White;
        }

        /// <summary>
        /// Applies ambient tint (sunrise/sunset) to a color.
        /// Blends the color toward the tint color.
        /// </summary>
        private ConsoleColor ApplyAmbientTint(ConsoleColor originalColor, (double r, double g, double b) tint)
        {
            // If tint is neutral (white), no change needed
            if (Math.Abs(tint.r - 1.0) < 0.01 && Math.Abs(tint.g - 1.0) < 0.01 && Math.Abs(tint.b - 1.0) < 0.01)
                return originalColor;

            // For sunrise/sunset (reddish tint), shift colors toward red/orange spectrum
            if (tint.r > tint.g && tint.r > tint.b)
            {
                // Reddish tint
                return originalColor switch
                {
                    ConsoleColor.White => ConsoleColor.Yellow,
                    ConsoleColor.Gray => ConsoleColor.DarkYellow,
                    ConsoleColor.Cyan => ConsoleColor.Green,
                    ConsoleColor.Blue => ConsoleColor.DarkCyan,
                    ConsoleColor.Green => ConsoleColor.DarkGreen,
                    ConsoleColor.Yellow => ConsoleColor.DarkYellow,
                    _ => originalColor // Keep others unchanged
                };
            }

            return originalColor;
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

        /// <summary>
        /// Captures the current rendered frame as a 2D array of strings for monitoring
        /// </summary>
        public AsciiMapData CaptureRenderedFrame()
        {
            if (Perception == null || WorldLocation == null)
            {
                return new AsciiMapData(0, 0);
            }

            var worldWidth = ContentSize.Width / symbolWidth;
            var worldHeight = ContentSize.Height;

            var asciiMap = new AsciiMapData(worldWidth, worldHeight);

            var xoffset = worldWidth / 2;
            var yoffset = worldHeight / 2;

            for (int y = 0; y < worldHeight; y++)
            {
                for (int x = 0; x < worldWidth; x++)
                {
                    // Calculate relative coordinates (offsets from player at center)
                    var relativeX = x - xoffset;
                    var relativeY = y - yoffset;
                    var relativeZ = 0;

                    // Create location key for looking up in perception visuals (using relative coordinates)
                    var locationKey = $"{relativeX},{relativeY},{relativeZ}";

                    // Check if this location is visible
                    if (!Perception.Visuals.TryGetValue(locationKey, out var visual))
                    {
                        asciiMap.Tiles[y][x] = "  "; // Empty space
                        continue;
                    }

                    // Check if this is the player location (always at 0,0,0 in relative coordinates)
                    if (relativeX == 0 && relativeY == 0 && relativeZ == 0)
                    {
                        // Player
                        if (Perception.TileTypes.TryGetValue("Player", out var playerTileType) &&
                            playerTileType.Settings.TryGetValue("MapCharacter", out var playerChar))
                        {
                            asciiMap.Tiles[y][x] = playerChar + playerChar; // 2 characters wide
                        }
                        else
                        {
                            asciiMap.Tiles[y][x] = "@@"; // Default player character
                        }
                    }
                    else
                    {
                        // Check for items at this location first
                        var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                            item => item.Location != null &&
                            item.Location.X == relativeX &&
                            item.Location.Y == relativeY &&
                            item.Location.Z == relativeZ);

                        if (itemAtLocation != null && !string.IsNullOrEmpty(itemAtLocation.Icon))
                        {
                            var icon = itemAtLocation.Icon;
                            if (icon.Length >= 2)
                                asciiMap.Tiles[y][x] = icon.Substring(0, 2);
                            else
                                asciiMap.Tiles[y][x] = icon.PadRight(2);
                        }
                        else if (visual.Terrain != null && visual.Terrain.Settings.TryGetValue("MapCharacter", out var terrainChar))
                        {
                            // Terrain - duplicate the character to fill 2 spaces
                            asciiMap.Tiles[y][x] = terrainChar + terrainChar;
                        }
                        else
                        {
                            asciiMap.Tiles[y][x] = "  "; // Unknown/empty
                        }
                    }
                }
            }

            return asciiMap;
        }
    }
}


