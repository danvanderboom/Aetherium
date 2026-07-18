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

        // Glyphs for off-focus cells that hold only an entity, no terrain — an overhead flyer, a creature below.
        private const string SilhouetteCharacter = "^";
        private const string SilhouetteObject = "*";

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

        // --- Depth compositing & level ribbon (add-adaptive-depth-visualization Section 2) ---

        // Groups the perception's visuals by screen column (relative x,y), each column sorted top band first.
        private Dictionary<(int x, int y), List<VisualDto>> BuildColumnIndex()
        {
            var index = new Dictionary<(int x, int y), List<VisualDto>>();
            if (Perception == null) return index;

            foreach (var v in Perception.Visuals.Values)
            {
                var key = (v.Location.X, v.Location.Y);
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<VisualDto>();
                    index[key] = list;
                }
                list.Add(v);
            }

            foreach (var list in index.Values)
                list.Sort((a, b) => b.Location.Z.CompareTo(a.Location.Z)); // top band first
            return index;
        }

        private HashSet<(int x, int y, int z)> BuildItemLocationSet()
        {
            var set = new HashSet<(int x, int y, int z)>();
            if (Perception?.VisibleItems == null) return set;
            foreach (var item in Perception.VisibleItems)
                if (item.Location != null)
                    set.Add((item.Location.X, item.Location.Y, item.Location.Z));
            return set;
        }

        // A cell anchors a glyph if it has terrain, an item, or something seen there. Empty focus cells (open air /
        // grates) are not drawable, so a lower band shows through.
        private static bool IsDrawable(VisualDto v, HashSet<(int x, int y, int z)> itemLocs) =>
            v.Terrain != null
            || v.ThingsSeen.Count > 0
            || itemLocs.Contains((v.Location.X, v.Location.Y, v.Location.Z));

        // Composite selection: the focus band (dZ 0) wins when drawable; otherwise the topmost drawable band in the
        // column — a deck overhead, or a floor seen down an open shaft. Null when the column holds no content.
        private static (VisualDto? visual, int dz) SelectDisplayVisual(
            List<VisualDto> columnTopFirst, HashSet<(int x, int y, int z)> itemLocs)
        {
            VisualDto? focus = null;
            foreach (var v in columnTopFirst)
                if (v.Location.Z == 0) { focus = v; break; }

            if (focus != null && IsDrawable(focus, itemLocs))
                return (focus, 0);

            foreach (var v in columnTopFirst) // already top band first
                if (IsDrawable(v, itemLocs))
                    return (v, v.Location.Z);

            return (null, 0);
        }

        // Depth attenuation applied to a cell's light, keyed on |dZ|: focus full, deeper bands progressively dimmer.
        private static double DepthFactor(int dz) => 1.0 / (1.0 + 0.5 * Math.Abs(dz));

        /// <summary>
        /// The occupied bands around the player (relative Z), top band first, for the HUD level ribbon. The focus
        /// band (0) is always included; each entry is flagged when it is the focus band.
        /// </summary>
        public List<(int dz, bool isFocus)> BuildLevelRibbon()
        {
            var bands = new SortedSet<int>();
            if (Perception != null)
            {
                var itemLocs = BuildItemLocationSet();
                foreach (var v in Perception.Visuals.Values)
                    if (IsDrawable(v, itemLocs))
                        bands.Add(v.Location.Z);
            }
            bands.Add(0);

            var result = new List<(int dz, bool isFocus)>();
            foreach (var z in bands.Reverse())
                result.Add((z, z == 0));
            return result;
        }

        private ConsoleColor? GridColorAt(int relativeX, int relativeY)
        {
            if (GridColoring == null) return null;
            var h = GridColoring.GetLength(0);
            var w = GridColoring.GetLength(1);
            return GridColoring[Math.Abs(relativeY % h), Math.Abs(relativeX % w)];
        }

        private static TileTypeDto FallbackPlayerTile() => new TileTypeDto
        {
            Name = "Player",
            Settings = new Dictionary<string, string>
            {
                ["MapCharacter"] = "@",
                ["ForegroundColor"] = "Yellow",
                ["BackgroundColor"] = "Black"
            }
        };

        private void DrawSilhouette(VisualDto visual, ConsoleColor? gridColor, double light)
        {
            var ch = visual.ThingsSeen.ContainsKey(Aetherium.Model.VisualType.Character) ? SilhouetteCharacter : SilhouetteObject;
            DrawTileType(new TileTypeDto
            {
                Name = "Silhouette",
                Settings = new Dictionary<string, string>
                {
                    ["MapCharacter"] = ch,
                    ["ForegroundColor"] = "DarkGray",
                    ["BackgroundColor"] = "Black"
                }
            }, gridColor, light);
        }

        // Draws the vertical level ribbon to the right of the map: one row per occupied band, focus band marked.
        private void DrawLevelRibbon(Point screenPosition, Size size)
        {
            var ribbon = BuildLevelRibbon();
            if (ribbon.Count <= 1) return; // a single band needs no depth ribbon

            int col = screenPosition.X + size.Width + 1;
            try
            {
                for (int i = 0; i < ribbon.Count && i < size.Height; i++)
                {
                    var (dz, isFocus) = ribbon[i];
                    Console.SetCursorPosition(col, screenPosition.Y + i);
                    Console.BackgroundColor = BackgroundColor;
                    Console.ForegroundColor = isFocus ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                    Console.Write((isFocus ? ">" : " ") + FormatBand(dz).PadLeft(3));
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Console too narrow for the side ribbon; the HUD is best-effort, so skip it.
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static string FormatBand(int dz) => (dz > 0 ? "+" : "") + dz.ToString();

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

            var columnIndex = BuildColumnIndex();
            var itemLocs = BuildItemLocationSet();

            // Focus-band light for the player glyph.
            double playerLight = 1.0;
            if (columnIndex.TryGetValue((0, 0), out var playerColumn))
            {
                var pf = playerColumn.Find(v => v.Location.Z == 0);
                if (pf != null) playerLight = pf.LightLevel;
            }

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width / symbolWidth; x++)
                {
                    Console.SetCursorPosition(screenPosition.X + (x * symbolWidth), screenPosition.Y + y);

                    // Relative coordinates (offsets from the player at the centre).
                    var relativeX = x - xoffset;
                    var relativeY = y - yoffset;
                    var color = GridColorAt(relativeX, relativeY);

                    // The player is always centred on the focus band.
                    if (relativeX == 0 && relativeY == 0)
                    {
                        if (Perception.TileTypes.TryGetValue("Player", out var playerTileType))
                            DrawTileType(playerTileType, color, playerLight);
                        else
                            DrawTileType(FallbackPlayerTile(), color, playerLight);
                        continue;
                    }

                    // Composite this screen column over the slab.
                    if (!columnIndex.TryGetValue((relativeX, relativeY), out var column))
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                        continue;
                    }

                    var (visual, dz) = SelectDisplayVisual(column, itemLocs);
                    if (visual == null)
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                        continue;
                    }

                    // Off-focus bands are attenuated by depth; the focus band renders at full lighting.
                    var light = visual.LightLevel * DepthFactor(dz);

                    var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                        item => item.Location != null &&
                        item.Location.X == relativeX &&
                        item.Location.Y == relativeY &&
                        item.Location.Z == dz);

                    if (itemAtLocation != null)
                        DrawItem(itemAtLocation, color, light);
                    else if (visual.Terrain != null)
                        DrawTileType(visual.Terrain, color, light);
                    else if (dz != 0 && visual.ThingsSeen.Count > 0)
                        DrawSilhouette(visual, color, light); // an overhead/below entity with no terrain
                    else
                    {
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', symbolWidth));
                    }
                }
            }

            DrawLevelRibbon(screenPosition, size);

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

            var columnIndex = BuildColumnIndex();
            var itemLocs = BuildItemLocationSet();

            for (int y = 0; y < worldHeight; y++)
            {
                for (int x = 0; x < worldWidth; x++)
                {
                    var relativeX = x - xoffset;
                    var relativeY = y - yoffset;

                    // Player is always centred on the focus band.
                    if (relativeX == 0 && relativeY == 0)
                    {
                        if (Perception.TileTypes.TryGetValue("Player", out var playerTileType) &&
                            playerTileType.Settings.TryGetValue("MapCharacter", out var playerChar))
                            asciiMap.Tiles[y][x] = playerChar + playerChar;
                        else
                            asciiMap.Tiles[y][x] = "@@";
                        continue;
                    }

                    if (!columnIndex.TryGetValue((relativeX, relativeY), out var column))
                    {
                        asciiMap.Tiles[y][x] = "  ";
                        continue;
                    }

                    var (visual, dz) = SelectDisplayVisual(column, itemLocs);
                    if (visual == null)
                    {
                        asciiMap.Tiles[y][x] = "  ";
                        continue;
                    }

                    var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                        item => item.Location != null &&
                        item.Location.X == relativeX &&
                        item.Location.Y == relativeY &&
                        item.Location.Z == dz);

                    if (itemAtLocation != null && !string.IsNullOrEmpty(itemAtLocation.Icon))
                    {
                        var icon = itemAtLocation.Icon;
                        asciiMap.Tiles[y][x] = icon.Length >= 2 ? icon.Substring(0, 2) : icon.PadRight(2);
                    }
                    else if (visual.Terrain != null && visual.Terrain.Settings.TryGetValue("MapCharacter", out var terrainChar))
                    {
                        asciiMap.Tiles[y][x] = terrainChar + terrainChar;
                    }
                    else if (dz != 0 && visual.ThingsSeen.Count > 0)
                    {
                        var ch = visual.ThingsSeen.ContainsKey(Aetherium.Model.VisualType.Character) ? SilhouetteCharacter : SilhouetteObject;
                        asciiMap.Tiles[y][x] = ch + ch;
                    }
                    else
                    {
                        asciiMap.Tiles[y][x] = "  ";
                    }
                }
            }

            return asciiMap;
        }
    }
}


