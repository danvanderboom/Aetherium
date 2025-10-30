using System;
using System.Threading.Tasks;
using Spectre.Console;
using ConsoleGame.Rendering.Themes;
using ConsoleGame.Rendering.Widgets;
using ConsoleGameModel;
using System.Linq;
using System.Drawing;

namespace ConsoleGame.Rendering
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

            // For now, use a simpler approach without live rendering for compatibility
            // We'll render directly to console with Spectre.Console panels
            AnsiConsole.Clear();

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

            // Render map section (we'll use direct console rendering for performance)
            RenderMapSection(state);

            // Render widgets on the right side
            RenderWidgets(state, mapWidth + 2, 2);
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

        private void RenderWidgets(GameViewState state, int startX, int startY)
        {
            int currentY = startY;

            // Render each visible widget
            foreach (var widget in state.Widgets.Values.Where(w => w.IsVisible).OrderBy(w => w.ZOrder))
            {
                var renderData = widget.GetRenderData();
                
                if (renderData is CompassRenderData compassData)
                {
                    RenderCompassWidget(compassData, state.Theme, startX, currentY);
                    currentY += 10; // Height of compass widget + spacing
                }
                else if (renderData is InventoryRenderData inventoryData)
                {
                    RenderInventoryWidget(inventoryData, state.Theme, startX, currentY);
                    currentY += 12; // Height of inventory widget + spacing
                }
            }

            // Help panel beneath widgets
            RenderHelpPanel(state.Theme, startX, currentY);
        }

        private void RenderCompassWidget(CompassRenderData data, ThemeConfig theme, int x, int y)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var content = new Rows(
                new Text(""),
                new Markup($"[bold]{data.DirectionSymbol}[/]", new Style(foreground: GetSpectreColor(theme.AccentColor))).Centered(),
                new Text(data.DirectionName).Centered(),
                new Text(""),
                new Text(data.Mode == CompassMode.Degree ? $"{data.Heading}°" : "").Centered(),
                new Text(""),
                new Markup($"[dim][M] Toggle Mode[/]").Centered()
            );

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]COMPASS[/]")
                .Border(borderStyle)
                .BorderColor(borderColor)
                .Expand();

            Console.SetCursorPosition(x, y);
            AnsiConsole.Write(panel);
        }

        private void RenderInventoryWidget(InventoryRenderData data, ThemeConfig theme, int x, int y)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var itemsList = string.Join("\n", data.Items.Select(item => $"• {item}"));
            if (string.IsNullOrEmpty(itemsList))
                itemsList = "[dim]Empty[/]";

            var content = new Rows(
                new Markup($"[{titleColor.ToMarkup()}]Capacity: {data.Count}/{data.Capacity}[/]"),
                new Text(""),
                new Markup(itemsList)
            );

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]INVENTORY[/]")
                .Border(borderStyle)
                .BorderColor(borderColor);

            Console.SetCursorPosition(x, y);
            AnsiConsole.Write(panel);
        }

        private void RenderHelpPanel(ThemeConfig theme, int x, int y)
        {
            var borderStyle = GetBoxBorder(theme.BorderStyle);
            var borderColor = GetSpectreColor(theme.BorderColor);
            var titleColor = GetSpectreColor(theme.GetColor("widget_title", ConsoleColor.White));

            var content = new Rows(
                new Markup("[bold]Controls[/]"),
                new Markup("Move: [yellow]WASD/Arrows[/]  Rotate: [yellow]Q/E[/]  Level: [yellow]R/F[/]"),
                new Markup("Pickup: [yellow]G[/]  Drop: [yellow]P[/]  Open: [yellow]O[/]  Close: [yellow]C[/]"),
                new Text(""),
                new Markup("Audio: [yellow]N[/] toggle  [yellow]Shift+M[/] next track  [yellow]M[/] compass mode")
            );

            var panel = new Panel(content)
                .Header($"[{titleColor.ToMarkup()}]HELP[/]")
                .Border(borderStyle)
                .BorderColor(borderColor)
                .Expand();

            Console.SetCursorPosition(x, y);
            AnsiConsole.Write(panel);
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

