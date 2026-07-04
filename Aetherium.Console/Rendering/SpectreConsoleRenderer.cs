using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;
using Aetherium.Rendering.Themes;
using Aetherium.Rendering.Widgets;
using Aetherium.Model;
using System.Linq;
using System.Drawing;

namespace Aetherium.Rendering
{
    /// <summary>
    /// IGameRenderer implementation using Spectre.Console for rich UI
    /// </summary>
    public class SpectreConsoleRenderer : IGameRenderer
    {
        private bool isInitialized;
        private Layout? mainLayout;
        private GameViewState? currentState;

        public void Initialize()
        {
            if (isInitialized)
                return;

            // Setup console
            try
            {
                if (!Console.IsOutputRedirected)
                    Console.CursorVisible = false;
            }
            catch
            {
                // Ignore platform-specific errors
            }

            System.Console.OutputEncoding = System.Text.Encoding.Unicode;
            AnsiConsole.Clear();

            isInitialized = true;
        }

        public void Shutdown()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                    Console.CursorVisible = true;
            }
            catch
            {
                // Ignore platform-specific errors
            }

            isInitialized = false;
        }

        public void RenderFrame(GameViewState state)
        {
            if (!isInitialized)
                Initialize();

            currentState = state;

            // Note: The map is rendered by `ClientConsoleMapView` before this call.
            // Do NOT clear the console here, or you'll erase the map.

            if (state.Perception == null || !state.IsConnected)
            {
                RenderDisconnectedState(state);
                return;
            }

            RenderConnectedState(state);
        }

        private void RenderDisconnectedState(GameViewState state)
        {
            var panel = new Panel("[red]Disconnected from server[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Spectre.Console.Color.Red);

            AnsiConsole.Write(panel);
        }

        private void RenderConnectedState(GameViewState state)
        {
            // Create main layout: map on left, widgets on right
            var mapWidth = 88; // 42 chars * 2 for double-width + borders
            var widgetWidth = 30;

            // Map is already rendered by `ClientConsoleMapView` in the game loop.
            // Before drawing widgets, clear the sidebar region to prevent artifacting
            // from previous frames (since we don't clear the whole console).
            var sidebarX = mapWidth + 2;
            ClearRegion(sidebarX, 0, widgetWidth + 4, Console.BufferHeight);

            // Render widgets on the right side
            RenderWidgets(state, sidebarX, 2, widgetWidth);
        }

        private void RenderMapSection(GameViewState state)
        {
            if (state.Perception == null)
                return;

            // Create a panel for the map
            var borderStyle = GetBoxBorder(state.Theme.BorderStyle);
            var borderColor = GetSpectreColor(state.Theme.BorderColor);

            // We'll render the map content using the existing ClientConsoleMapView logic
            // For now, just create a placeholder panel
            var mapPanel = new Panel("[grey]Map rendering (using existing view)[/]")
                .Header("[yellow]Game View[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            // Position cursor and render
            Console.SetCursorPosition(2, 2);
            AnsiConsole.Write(mapPanel);

            // Note: The actual map tiles will be rendered by ClientConsoleMapView
            // We're creating a hybrid approach here
        }

        private void RenderWidgets(GameViewState state, int startX, int startY, int width)
        {
            int currentY = startY;

            // Render each visible widget
            foreach (var widget in state.Widgets.Values.Where(w => w.IsVisible).OrderBy(w => w.ZOrder))
            {
                var renderData = widget.GetRenderData();
                
                if (renderData is CompassRenderData compassData)
                {
                    RenderCompassWidget(compassData, state.Theme, startX, currentY, width);
                    currentY += 10; // Height of compass widget + spacing
                }
                else if (renderData is InventoryRenderData inventoryData)
                {
                    RenderInventoryWidget(inventoryData, state.Theme, startX, currentY, width);
                    currentY += 12; // Height of inventory widget + spacing
                }
            }

            // Status line (pickup results, failure reasons, mode changes). Was never
            // rendered anywhere before — every piece of feedback was invisible.
            if (!string.IsNullOrWhiteSpace(state.StatusMessage))
            {
                RenderStatusPanel(state.StatusMessage, state.Theme, startX, currentY, width);
                currentY += 4;
            }

            // Help panel beneath widgets - add some spacing
            currentY += 2;
            RenderHelpPanel(state.Theme, startX, currentY, width);
        }

        /// <summary>
        /// Builds the inventory item list as safe Spectre markup. Labels and key ids
        /// come from server data and can contain <c>[...]</c> (e.g. "[gold-key]"),
        /// which Markup would otherwise parse as a style tag — crashing the frame on
        /// unknown tags or silently restyling the text on known ones.
        /// </summary>
        public static string BuildInventoryItemsMarkup(InventoryRenderData data)
        {
            var itemsList = string.Join("\n", data.Items.Select(item => $"• {Markup.Escape(item)}"));
            return string.IsNullOrEmpty(itemsList) ? "[dim]Empty[/]" : itemsList;
        }

        /// <summary>
        /// Builds the status-line markup, escaping the message for the same reason
        /// as <see cref="BuildInventoryItemsMarkup"/> — status text echoes item
        /// labels and server-supplied failure reasons.
        /// </summary>
        public static string BuildStatusMarkup(string statusMessage)
            => Markup.Escape(statusMessage);

        private void RenderStatusPanel(string statusMessage, ThemeConfig theme, int x, int y, int width)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var panel = new Panel(new Markup(BuildStatusMarkup(statusMessage)))
                .Header($"[{titleColor.ToMarkup()}]STATUS[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            WriteAtWithWidth(panel, x, y, width);
        }

        private void RenderCompassWidget(CompassRenderData data, ThemeConfig theme, int x, int y, int width)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            // Build content rows
            var rows = new List<IRenderable>
            {
                new Text(""),
                new Markup($"[bold]{data.DirectionSymbol}[/]", new Style(foreground: GetSpectreColor(theme.AccentColor))).Centered(),
                new Text(data.DirectionName).Centered(),
                new Text(""),
                new Text(data.Mode == CompassMode.Degree ? $"{data.Heading}°" : "").Centered()
            };

            // Add directional vision indicator if enabled
            if (data.IsDirectionalVision)
            {
                rows.Add(new Text(""));
                rows.Add(new Markup($"[yellow]◢ FOV: {data.FieldOfViewDegrees}° ◣[/]").Centered());
            }

            rows.Add(new Text(""));
            rows.Add(new Markup($"[dim][[M]] Toggle Mode[/]").Centered());

            var content = new Rows(rows);

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]COMPASS[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            WriteAtWithWidth(panel, x, y, width);
        }

        private void RenderInventoryWidget(InventoryRenderData data, ThemeConfig theme, int x, int y, int width)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var itemsList = BuildInventoryItemsMarkup(data);

            var content = new Rows(
                new Markup($"[{titleColor.ToMarkup()}]Capacity: {data.Count}/{data.Capacity}[/]"),
                new Text(""),
                new Markup(itemsList)
            );

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]INVENTORY[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            WriteAtWithWidth(panel, x, y, width);
        }

        private void RenderHelpPanel(ThemeConfig theme, int x, int y, int width)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var content = new Rows(
                new Markup("Move: [yellow]WASD/Arrows[/]  Rotate: [yellow]Q/E[/]  Level: [yellow]R/F[/]"),
                new Markup("Pickup: [yellow]G[/]  Drop: [yellow]P[/]  Open: [yellow]O[/]  Close: [yellow]C[/]"),
                new Markup("Audio: [yellow]N[/] toggle  [yellow]Shift+M[/] next track  [yellow]M[/] compass mode")
            );

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]HELP[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            // Position the panel properly
            WriteAtWithWidth(panel, x, y, width);
        }

        private void WriteAtWithWidth(IRenderable renderable, int x, int y, int width)
        {
            // Create a fixed-width container
            var table = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .NoBorder();
            
            table.AddColumn(new TableColumn("").Width(width));
            table.AddRow(renderable);

            // Render to string using a memory-based console
            var stringWriter = new System.IO.StringWriter();
            var tempConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.TrueColor,
                Out = new AnsiConsoleOutput(stringWriter),
                Interactive = InteractionSupport.No
            });

            tempConsole.Write(table);
            
            // Get the rendered output and write it line by line at the correct position
            var output = stringWriter.ToString();
            var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            int currentY = y;
            foreach (var line in lines)
            {
                if (currentY >= Console.BufferHeight || string.IsNullOrEmpty(line))
                {
                    if (!string.IsNullOrEmpty(line)) break;
                    continue;
                }
                
                if (x >= 0 && currentY >= 0)
                {
                    Console.SetCursorPosition(x, currentY);
                    // Strip ANSI to calculate visible length, but write the original line with ANSI codes
                    var visibleText = StripAnsi(line);
                    var visibleLength = visibleText.Length;
                    
                    // Write the line with ANSI codes intact, then pad with spaces
                    if (visibleLength <= width)
                    {
                        Console.Write(line);
                        Console.Write(new string(' ', width - visibleLength));
                    }
                    else
                    {
                        // Truncate if too long (rare, but possible)
                        Console.Write(visibleText.Substring(0, width));
                    }
                }
                currentY++;
            }
            
            stringWriter.Dispose();
        }

        private void ClearRegion(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            var blank = new string(' ', Math.Max(0, Math.Min(width, Math.Max(0, Console.BufferWidth - x))));
            for (int row = 0; row < height; row++)
            {
                if (y + row >= Console.BufferHeight) break;
                Console.SetCursorPosition(Math.Min(x, Console.BufferWidth - 1), y + row);
                Console.Write(blank);
            }
        }

        private string StripAnsi(string input)
        {
            // Remove ANSI escape sequences when computing visible length
            // Regex: \x1B\[[0-9;]*[A-Za-z]
            var pattern = "\u001B\\[[0-9;]*[A-Za-z]";
            try
            {
                return System.Text.RegularExpressions.Regex.Replace(input, pattern, "");
            }
            catch
            {
                return input;
            }
        }

        private BoxBorder GetBoxBorder(string style)
        {
            return style.ToLowerInvariant() switch
            {
                "ascii" => BoxBorder.Ascii,
                "double" => BoxBorder.Double,
                "heavy" => BoxBorder.Heavy,
                "rounded" => BoxBorder.Rounded,
                "square" => BoxBorder.Square,
                _ => BoxBorder.Rounded
            };
        }

        private Spectre.Console.Color GetSpectreColor(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Black => Spectre.Console.Color.Black,
                ConsoleColor.DarkBlue => Spectre.Console.Color.Blue,
                ConsoleColor.DarkGreen => Spectre.Console.Color.Green,
                ConsoleColor.DarkCyan => Spectre.Console.Color.Aqua,
                ConsoleColor.DarkRed => Spectre.Console.Color.Maroon,
                ConsoleColor.DarkMagenta => Spectre.Console.Color.Purple,
                ConsoleColor.DarkYellow => Spectre.Console.Color.Olive,
                ConsoleColor.Gray => Spectre.Console.Color.Silver,
                ConsoleColor.DarkGray => Spectre.Console.Color.Grey,
                ConsoleColor.Blue => Spectre.Console.Color.Blue,
                ConsoleColor.Green => Spectre.Console.Color.Lime,
                ConsoleColor.Cyan => Spectre.Console.Color.Aqua,
                ConsoleColor.Red => Spectre.Console.Color.Red,
                ConsoleColor.Magenta => Spectre.Console.Color.Fuchsia,
                ConsoleColor.Yellow => Spectre.Console.Color.Yellow,
                ConsoleColor.White => Spectre.Console.Color.White,
                _ => Spectre.Console.Color.White
            };
        }

        public ConsoleKeyInfo? GetInputCommand()
        {
            if (Console.KeyAvailable)
            {
                return Console.ReadKey(true);
            }
            return null;
        }

        public async Task<ConsoleKeyInfo> WaitForInputCommandAsync()
        {
            while (!Console.KeyAvailable)
            {
                await Task.Delay(50);
            }
            return Console.ReadKey(true);
        }

        public void Clear()
        {
            AnsiConsole.Clear();
        }
    }

    /// <summary>
    /// Render data for compass widget
    /// </summary>
    public class CompassRenderData
    {
        public CompassMode Mode { get; set; }
        public int Heading { get; set; }
        public string DirectionName { get; set; } = "";
        public string DirectionSymbol { get; set; } = "";
        public bool IsDirectionalVision { get; set; }
        public int FieldOfViewDegrees { get; set; } = 360;
    }

    /// <summary>
    /// Render data for inventory widget
    /// </summary>
    public class InventoryRenderData
    {
        public int Count { get; set; }
        public int Capacity { get; set; }
        public string[] Items { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Compass display mode
    /// </summary>
    public enum CompassMode
    {
        Arrow,
        Degree
    }
}


