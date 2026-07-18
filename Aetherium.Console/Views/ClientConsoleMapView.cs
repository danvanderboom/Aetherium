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

        /// <summary>When true, the map renders as a side-on elevation (cross-section) instead of the top-down plan.</summary>
        public bool CrossSectionMode { get; set; } = false;

        /// <summary>
        /// When true, the elevation view is auto-surfaced once the local column's vertical complexity crosses
        /// <see cref="CrossSectionEscalationThreshold"/> — Section 5.2 mode escalation. Off by default so the
        /// manual <c>X</c> toggle stays in full control unless a client opts in.
        /// </summary>
        public bool AutoEscalateCrossSection { get; set; } = false;

        /// <summary>Occupied-band count at/above which the elevation view auto-surfaces (when auto-escalation is on).</summary>
        public int CrossSectionEscalationThreshold { get; set; } = 4;

        /// <summary>
        /// Distinct occupied bands in the current perception slab (including the focus band) — the local
        /// vertical complexity that drives mode escalation. Reuses the level-ribbon band set.
        /// </summary>
        public int VerticalComplexity() => BuildLevelRibbon().Count;

        /// <summary>True when vertical complexity warrants auto-surfacing the elevation view.</summary>
        public bool ShouldAutoSurfaceCrossSection() =>
            VerticalComplexity() >= CrossSectionEscalationThreshold;

        /// <summary>
        /// Whether the elevation view should render this frame: the manual toggle, or auto-escalation past the
        /// threshold when enabled. The plan view is the default otherwise.
        /// </summary>
        public bool EffectiveCrossSection =>
            CrossSectionMode || (AutoEscalateCrossSection && ShouldAutoSurfaceCrossSection());

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

        /// <summary>
        /// The altitude gauge for a flying player (Section 5.3): one rung per band from `MaxBand` (top)
        /// down to `MinBand`, flagging the band the player currently occupies. Empty when the perceiver has
        /// no flight envelope (they are not flying/piloting). Pure — safe to unit test.
        /// </summary>
        public List<(int band, bool isCurrent)> BuildAltitudeGauge()
        {
            var rungs = new List<(int band, bool isCurrent)>();
            var env = Perception?.FlightEnvelope;
            if (env == null)
                return rungs;

            int min = Math.Min(env.MinBand, env.MaxBand);
            int max = Math.Max(env.MinBand, env.MaxBand);
            for (int b = max; b >= min; b--)
                rungs.Add((b, b == env.CurrentBand));
            return rungs;
        }

        /// <summary>
        /// The altitude gauge rendered as text rungs (top band first), current band marked with '&gt;', others
        /// with a '|' rail — e.g. "|+5", "&gt;+4", "| 0". Empty when not flying. Pure — no Console writes.
        /// </summary>
        public List<string> RenderAltitudeGaugeLines()
        {
            var lines = new List<string>();
            foreach (var (band, isCurrent) in BuildAltitudeGauge())
                lines.Add((isCurrent ? ">" : "|") + FormatBand(band).PadLeft(3));
            return lines;
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

        /// <summary>
        /// Draws the altitude gauge (Section 5.3) as a vertical ladder just right of the level ribbon, shown
        /// only while the player is flying/piloting. The current band rung is highlighted. Best-effort HUD —
        /// skipped silently if the console is too narrow.
        /// </summary>
        private void DrawAltitudeGauge(Point screenPosition, Size size)
        {
            var rungs = BuildAltitudeGauge();
            if (rungs.Count == 0) return;

            int col = screenPosition.X + size.Width + 6; // clear of the 4-wide level ribbon
            try
            {
                for (int i = 0; i < rungs.Count && i < size.Height; i++)
                {
                    var (band, isCurrent) = rungs[i];
                    Console.SetCursorPosition(col, screenPosition.Y + i);
                    Console.BackgroundColor = BackgroundColor;
                    Console.ForegroundColor = isCurrent ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
                    Console.Write((isCurrent ? ">" : "|") + FormatBand(band).PadLeft(3));
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Console too narrow for the gauge; the HUD is best-effort, so skip it.
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        // --- Cross-section / elevation view (Section 4) ---

        private string PlayerGlyph()
        {
            if (Perception != null
                && Perception.TileTypes.TryGetValue("Player", out var playerTile)
                && playerTile.Settings.TryGetValue("MapCharacter", out var ch)
                && !string.IsNullOrEmpty(ch))
                return (ch.Length >= 2 ? ch.Substring(0, 2) : ch + ch);
            return "@@";
        }

        // The 2-char glyph for a cell in the elevation schematic (terrain, item, or entity silhouette; else void).
        private string CrossSectionGlyph(VisualDto? v, int dx, int z, HashSet<(int x, int y, int z)> itemLocs)
        {
            if (v == null || !IsDrawable(v, itemLocs))
                return "  ";

            if (itemLocs.Contains((dx, 0, z)))
            {
                var item = Perception?.VisibleItems?.FirstOrDefault(
                    it => it.Location != null && it.Location.X == dx && it.Location.Y == 0 && it.Location.Z == z);
                if (item != null && !string.IsNullOrEmpty(item.Icon))
                    return item.Icon.Length >= 2 ? item.Icon.Substring(0, 2) : item.Icon.PadRight(2);
            }

            if (v.Terrain != null && v.Terrain.Settings.TryGetValue("MapCharacter", out var terrainChar))
                return terrainChar + terrainChar;

            if (v.ThingsSeen.Count > 0)
            {
                var ch = v.ThingsSeen.ContainsKey(Aetherium.Model.VisualType.Character) ? SilhouetteCharacter : SilhouetteObject;
                return ch + ch;
            }

            return "  ";
        }

        /// <summary>
        /// A side-on elevation of the bands around the player, sliced along the east-west axis at the player's row.
        /// One entry per occupied band (top band first; the focus band is always present and flagged), each with the
        /// concatenated glyphs across <paramref name="halfWidth"/> cells either side of the player. No per-tile FOV —
        /// it is a schematic projection of whatever the perception slab already contains.
        /// </summary>
        public List<(int band, bool isFocus, string cells)> BuildCrossSection(int halfWidth)
        {
            var rows = new List<(int band, bool isFocus, string cells)>();
            if (Perception == null) return rows;

            var itemLocs = BuildItemLocationSet();

            var bands = new SortedSet<int>();
            foreach (var v in Perception.Visuals.Values)
                if (v.Location.Y == 0 && Math.Abs(v.Location.X) <= halfWidth && IsDrawable(v, itemLocs))
                    bands.Add(v.Location.Z);
            bands.Add(0); // the focus band is always part of the elevation

            foreach (var z in bands.Reverse()) // top band first
            {
                var sb = new System.Text.StringBuilder();
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    if (dx == 0 && z == 0)
                    {
                        sb.Append(PlayerGlyph());
                        continue;
                    }
                    Perception.Visuals.TryGetValue($"{dx},0,{z}", out var v);
                    sb.Append(CrossSectionGlyph(v, dx, z, itemLocs));
                }
                rows.Add((z, z == 0, sb.ToString()));
            }
            return rows;
        }

        private void DrawCrossSection(Point screenPosition, Size size)
        {
            // Clear the content area.
            Console.BackgroundColor = BackgroundColor;
            var blank = new string(' ', size.Width);
            for (int y = 0; y < size.Height; y++)
            {
                try { Console.SetCursorPosition(screenPosition.X, screenPosition.Y + y); } catch { break; }
                Console.Write(blank);
            }

            const int labelWidth = 5; // "> +3 "
            const int cellWidth = 2;
            int halfWidth = Math.Max(1, ((size.Width - labelWidth) / cellWidth) / 2);
            var rows = BuildCrossSection(halfWidth);

            int startY = screenPosition.Y + Math.Max(1, (size.Height - rows.Count) / 2);

            for (int i = 0; i < rows.Count && startY + i < screenPosition.Y + size.Height; i++)
            {
                var (band, isFocus, cells) = rows[i];
                try { Console.SetCursorPosition(screenPosition.X, startY + i); } catch { break; }

                Console.BackgroundColor = BackgroundColor;
                Console.ForegroundColor = isFocus ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                Console.Write((isFocus ? ">" : " ") + FormatBand(band).PadLeft(3) + " ");

                Console.ForegroundColor = isFocus ? ConsoleColor.White : ConsoleColor.Gray;
                Console.Write(cells);
            }

            try
            {
                Console.SetCursorPosition(screenPosition.X, screenPosition.Y);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(CenterText("[Elevation view - press X for plan]", size.Width));
            }
            catch { /* narrow console */ }
            Console.ForegroundColor = ConsoleColor.Gray;
        }

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

            // Elevation (cross-section) view is a different projection of the same perception; render and return.
            // Either the manual toggle or auto-escalation past the vertical-complexity threshold surfaces it.
            if (EffectiveCrossSection)
            {
                DrawCrossSection(screenPosition, size);
                return;
            }

            // Update local heading from perception
            // Note: PlayerLocation is always (0,0,0) - we use relative coordinates only
            Heading = Perception.PlayerHeading.ToClientDirection();
            WorldLocation = Perception.PlayerLocation; // This is always (0,0,0) for relative coordinates

            // The server names the world's tiling; all cell-placement math below comes from the
            // shared GridCellLayout so square/hex/tri render from the same relative keys.
            var topology = Perception.Topology;
            symbolWidth = GridCellLayout.CellCharWidth(topology);

            var worldWidth = size.Width / symbolWidth;
            var worldHeight = size.Height;

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
                var relativeY = y - yoffset;

                // Hex rows with odd relY shift right half a cell (one character); blank the
                // leading character so the stagger never leaves a stale glyph behind.
                var stagger = GridCellLayout.RowStaggerChars(topology, relativeY);
                if (stagger > 0)
                {
                    Console.SetCursorPosition(screenPosition.X, screenPosition.Y + y);
                    Console.BackgroundColor = BackgroundColor;
                    Console.Write(new string(' ', stagger));
                }

                for (int x = 0; x < worldWidth; x++)
                {
                    var charColumn = x * symbolWidth + stagger;
                    if (charColumn + symbolWidth > size.Width)
                    {
                        // A staggered row's last cell doesn't fit; blank what remains of the row.
                        Console.SetCursorPosition(screenPosition.X + charColumn, screenPosition.Y + y);
                        Console.BackgroundColor = BackgroundColor;
                        Console.Write(new string(' ', size.Width - charColumn));
                        break;
                    }
                    Console.SetCursorPosition(screenPosition.X + charColumn, screenPosition.Y + y);

                    var relativeX = GridCellLayout.RelXForCellIndex(topology, x, relativeY, xoffset);
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

                    if (dz == 0)
                    {
                        // Focus band, full lighting. Characters (monsters/NPCs, other players) draw over items —
                        // a monster standing on treasure is the thing you need to see — which draw over terrain.
                        var characterAtLocation = Perception.VisibleCharacters?.FirstOrDefault(
                            c => c.Location != null && c.Location.X == relativeX && c.Location.Y == relativeY && c.Location.Z == 0);
                        var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                            i => i.Location != null && i.Location.X == relativeX && i.Location.Y == relativeY && i.Location.Z == 0);

                        switch (ResolveContentLayer(characterAtLocation != null, itemAtLocation != null, visual.Terrain != null))
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
                    else
                    {
                        // Off-focus band, attenuated by depth: character > item > terrain > silhouette.
                        var light = visual.LightLevel * DepthFactor(dz);
                        var characterAtLocation = Perception.VisibleCharacters?.FirstOrDefault(
                            c => c.Location != null && c.Location.X == relativeX && c.Location.Y == relativeY && c.Location.Z == dz);
                        var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                            i => i.Location != null && i.Location.X == relativeX && i.Location.Y == relativeY && i.Location.Z == dz);

                        if (characterAtLocation != null)
                            DrawCharacter(characterAtLocation, color, light);
                        else if (itemAtLocation != null)
                            DrawItem(itemAtLocation, color, light);
                        else if (visual.Terrain != null)
                            DrawTileType(visual.Terrain, color, light);
                        else if (visual.ThingsSeen.Count > 0)
                            DrawSilhouette(visual, color, light); // an overhead/below entity with no terrain
                        else
                        {
                            Console.BackgroundColor = BackgroundColor;
                            Console.Write(new string(' ', symbolWidth));
                        }
                    }
                }
            }

            DrawLevelRibbon(screenPosition, size);
            DrawAltitudeGauge(screenPosition, size);

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

            var tilingSuffix = string.IsNullOrEmpty(topology) || topology == "square"
                ? ""
                : $", {topology} tiling";
            Console.Write(
                CenterText($"Player Position: (0, 0, 0) [relative coordinates only{tilingSuffix}]",
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

        /// <summary>
        /// A colored key's glyph+color, keyed off <see cref="ItemDto.KeyId"/>. Color alone isn't
        /// colorblind-safe for which key this is (SemanticDistinction "item-key-color" — see
        /// wire-accessibility-live design.md), so every key color also gets a distinct glyph: its
        /// own first letter. Pure so it's unit-testable without a live console.
        /// </summary>
        public static (string Icon, ConsoleColor Color) ResolveKeyItemGlyph(string keyId)
        {
            var color = keyId.ToLowerInvariant() switch
            {
                "red" => ConsoleColor.Red,
                "blue" => ConsoleColor.Blue,
                "green" => ConsoleColor.Green,
                "yellow" => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };
            var icon = keyId.Length > 0 ? keyId.Substring(0, 1).ToUpperInvariant() : "?";
            return (icon, color);
        }

        private void DrawItem(ItemDto item, ConsoleColor? gridColor = null, double lightLevel = 1.0)
        {
            ConsoleColor fgColor = ConsoleColor.White;
            var icon = item.Icon;
            if (!string.IsNullOrEmpty(item.KeyId))
                (icon, fgColor) = ResolveKeyItemGlyph(item.KeyId);

            var bgColor = gridColor ?? ConsoleColor.Black;
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

            var topology = Perception.Topology;
            symbolWidth = GridCellLayout.CellCharWidth(topology);

            var worldWidth = ContentSize.Width / symbolWidth;
            var worldHeight = ContentSize.Height;

            var asciiMap = new AsciiMapData(worldWidth, worldHeight);

            var xoffset = worldWidth / 2;
            var yoffset = worldHeight / 2;

            // A cell glyph repeated/truncated to the topology's cell width.
            string Cell(string glyph) => glyph.Length >= symbolWidth
                ? glyph.Substring(0, symbolWidth)
                : string.Concat(Enumerable.Repeat(glyph.Length > 0 ? glyph : " ", symbolWidth)).Substring(0, symbolWidth);

            var blank = new string(' ', symbolWidth);

            var columnIndex = BuildColumnIndex();
            var itemLocs = BuildItemLocationSet();

            for (int y = 0; y < worldHeight; y++)
            {
                for (int x = 0; x < worldWidth; x++)
                {
                    // Same cell mapping as DrawContents (the capture is a cell grid, so the hex
                    // half-character stagger is dropped but cell content stays aligned).
                    var relativeY = y - yoffset;
                    var relativeX = GridCellLayout.RelXForCellIndex(topology, x, relativeY, xoffset);

                    // Player is always centred on the focus band.
                    if (relativeX == 0 && relativeY == 0)
                    {
                        if (Perception.TileTypes.TryGetValue("Player", out var playerTileType) &&
                            playerTileType.Settings.TryGetValue("MapCharacter", out var playerChar))
                            asciiMap.Tiles[y][x] = Cell(playerChar);
                        else
                            asciiMap.Tiles[y][x] = Cell("@");
                        continue;
                    }

                    if (!columnIndex.TryGetValue((relativeX, relativeY), out var column))
                    {
                        asciiMap.Tiles[y][x] = blank;
                        continue;
                    }

                    var (visual, dz) = SelectDisplayVisual(column, itemLocs);
                    if (visual == null)
                    {
                        asciiMap.Tiles[y][x] = blank;
                        continue;
                    }

                    var itemAtLocation = Perception.VisibleItems?.FirstOrDefault(
                        item => item.Location != null &&
                        item.Location.X == relativeX &&
                        item.Location.Y == relativeY &&
                        item.Location.Z == dz);

                    if (itemAtLocation != null && !string.IsNullOrEmpty(itemAtLocation.Icon))
                    {
                        asciiMap.Tiles[y][x] = Cell(itemAtLocation.Icon);
                    }
                    else if (visual.Terrain != null && visual.Terrain.Settings.TryGetValue("MapCharacter", out var terrainChar))
                    {
                        asciiMap.Tiles[y][x] = Cell(terrainChar);
                    }
                    else if (dz != 0 && visual.ThingsSeen.Count > 0)
                    {
                        var ch = visual.ThingsSeen.ContainsKey(Aetherium.Model.VisualType.Character) ? SilhouetteCharacter : SilhouetteObject;
                        asciiMap.Tiles[y][x] = Cell(ch);
                    }
                    else
                    {
                        asciiMap.Tiles[y][x] = blank;
                    }
                }
            }

            return asciiMap;
        }
    }
}
