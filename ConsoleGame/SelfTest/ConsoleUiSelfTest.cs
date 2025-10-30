using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ConsoleGame.Client;
using ConsoleGame.Rendering;
using ConsoleGame.Rendering.Themes;
using ConsoleGame.Rendering.Widgets;
using ConsoleGame.Views;
using ConsoleGameModel;

namespace ConsoleGame.SelfTest
{
    internal sealed class ConsoleUiSelfTest
    {
        private readonly string serverUrl;
        private readonly string artifactsDir;

        public ConsoleUiSelfTest(string serverUrl = "http://localhost:5000/gamehub", string artifactsDir = ".ui-test")
        {
            this.serverUrl = serverUrl;
            this.artifactsDir = artifactsDir;
        }

        public async Task<int> RunMoveDownScenarioAsync()
        {
            Directory.CreateDirectory(artifactsDir);

            var theme = BuiltInThemes.GetByName("zen");
            var renderer = new SpectreConsoleRenderer();

            var mapView = new ClientConsoleMapView
            {
                ScreenPosition = new Point(3, 2),
                Size = new Size(42, 22),
                BackgroundColor = ConsoleColor.Black,
                HasFrame = true,
                FrameBackgroundColor = ConsoleColor.DarkGray,
                FrameForegroundColor = ConsoleColor.Black
            };

            var widgetManager = new WidgetManager();
            var compassWidget = new CompassWidget(theme);
            var inventoryWidget = new InventoryWidget(theme);
            widgetManager.RegisterWidget(compassWidget);
            widgetManager.RegisterWidget(inventoryWidget);

            var gameClient = new GameClient(serverUrl);

            PerceptionDto? lastPerception = null;
            var tcsPerception = new TaskCompletionSource<PerceptionDto>();

            gameClient.PerceptionUpdated += p =>
            {
                lastPerception = p;
                if (!tcsPerception.Task.IsCompleted)
                    tcsPerception.TrySetResult(p);
            };

            await gameClient.ConnectAsync();

            // Wait for first perception
            var first = await tcsPerception.Task;
            mapView.Perception = first;
            mapView.WorldLocation = first.PlayerLocation;
            widgetManager.UpdateFromPerception(first);

            // Render initial frame
            mapView.Clear(clearFrame: true);
            mapView.DrawFrame();
            mapView.DrawContents();
            var state = new GameViewState
            {
                Perception = first,
                Widgets = widgetManager.GetAllWidgets(),
                Theme = theme,
                IsConnected = true,
                StatusMessage = "SelfTest",
                Timestamp = DateTime.UtcNow
            };
            renderer.RenderFrame(state);

            // Snapshot before
            var beforeLines = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            File.WriteAllLines(Path.Combine(artifactsDir, "before.txt"), beforeLines);

            // Move down once
            await gameClient.MovePlayerAsync(RelativeDirection.Backward, 1);

            // Wait briefly for perception to update
            await Task.Delay(200);
            var after = lastPerception;
            if (after == null)
                after = first;

            mapView.Perception = after;
            mapView.WorldLocation = after.PlayerLocation;
            widgetManager.UpdateFromPerception(after);

            mapView.Clear(clearFrame: true);
            mapView.DrawFrame();
            mapView.DrawContents();
            state.Perception = after;
            renderer.RenderFrame(state);

            var afterLines = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            File.WriteAllLines(Path.Combine(artifactsDir, "after.txt"), afterLines);

            // Analyze map content area (exclude frame):
            int contentLeft = mapView.ScreenPosition.X + 1;
            int contentTop = mapView.ScreenPosition.Y + 1;
            int contentWidth = mapView.Size.Width - 2;
            int contentHeight = mapView.Size.Height - 2;

            var beforeContent = Crop(beforeLines, contentLeft - mapView.ScreenPosition.X, contentTop - mapView.ScreenPosition.Y, contentWidth, contentHeight);
            var afterContent = Crop(afterLines, contentLeft - mapView.ScreenPosition.X, contentTop - mapView.ScreenPosition.Y, contentWidth, contentHeight);

            var result = EvaluateHeuristics(beforeContent, afterContent, artifactsDir);
            return result ? 0 : 1;
        }

        private static string[] Crop(string[] full, int left, int top, int width, int height)
        {
            var lines = new List<string>(height);
            for (int y = 0; y < height; y++)
            {
                var src = (top + y) < full.Length ? full[top + y] : string.Empty;
                if (src.Length < left) { lines.Add(new string(' ', width)); continue; }
                var slice = src.Substring(left, Math.Min(width, Math.Max(0, src.Length - left)));
                if (slice.Length < width) slice = slice + new string(' ', width - slice.Length);
                lines.Add(slice);
            }
            return lines.ToArray();
        }

        private static bool EvaluateHeuristics(string[] before, string[] after, string artifactsDir)
        {
            // 1) After must not be mostly spaces (blank)
            int total = after.Sum(l => l.Length);
            int spaces = after.Sum(l => l.Count(ch => ch == ' '));
            double spaceRatio = total == 0 ? 1.0 : (double)spaces / total;
            bool notBlank = spaceRatio < 0.95; // at least 5% non-space

            // 2) Map content should not contain obvious widget text
            string joined = string.Join("\n", after);
            var widgetKeywords = new[] { "HELP", "Move:", "Pickup:", "Audio:", "next track" };
            bool noWidgetBleed = !widgetKeywords.Any(k => joined.Contains(k, StringComparison.OrdinalIgnoreCase));

            // 3) The top two content rows should be fairly sparse of letters (widgets bleed left tends to put many)
            var topRows = after.Take(Math.Min(2, after.Length)).ToArray();
            int letters = topRows.Sum(r => r.Count(ch => char.IsLetter(ch)));
            bool topLooksLikeMap = letters < (topRows.Sum(r => r.Length) * 0.15); // <15% letters

            bool pass = notBlank && noWidgetBleed && topLooksLikeMap;

            if (!pass)
            {
                File.WriteAllText(Path.Combine(artifactsDir, "result.txt"),
                    $"notBlank={notBlank}, noWidgetBleed={noWidgetBleed}, topLooksLikeMap={topLooksLikeMap}\n");
            }
            return pass;
        }
    }
}


