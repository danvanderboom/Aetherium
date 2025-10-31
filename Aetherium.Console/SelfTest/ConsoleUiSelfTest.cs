using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aetherium.Client;
using Aetherium.Rendering;
using Aetherium.Rendering.Themes;
using Aetherium.Rendering.Widgets;
using Aetherium.Views;
using Aetherium.Model;

namespace Aetherium.SelfTest
{
    internal sealed class ConsoleUiSelfTest
    {
        private readonly string serverUrl;
        private readonly string artifactsDir;

        public ConsoleUiSelfTest(string serverUrl = "http://localhost:5000/gamehub", string? artifactsDir = null)
        {
            this.serverUrl = serverUrl;
            // If not provided, find project root and use .ui-test there
            if (artifactsDir == null)
            {
                var current = new DirectoryInfo(Environment.CurrentDirectory);
                while (current != null && !File.Exists(Path.Combine(current.FullName, "Aetherium.sln")))
                {
                    current = current.Parent;
                }
                if (current != null)
                {
                    this.artifactsDir = Path.Combine(current.FullName, ".ui-test");
                }
                else
                {
                    this.artifactsDir = Path.Combine(Environment.CurrentDirectory, ".ui-test");
                }
            }
            else
            {
                this.artifactsDir = artifactsDir;
            }
        }

        public async Task<int> RunMoveDownScenarioAsync()
        {
            try
            {
                Console.WriteLine($"[DEBUG] Artifacts directory: {artifactsDir}");
                Directory.CreateDirectory(artifactsDir);
                Console.WriteLine($"[SelfTest] Writing artifacts to: {Path.GetFullPath(artifactsDir)}");
                
                // Enable diagnostic mode in map view rendering
                Environment.SetEnvironmentVariable("UI_SELFTEST_MODE", "1");

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
            var tcsFirstPerception = new TaskCompletionSource<PerceptionDto>();
            var tcsSecondPerception = new TaskCompletionSource<PerceptionDto>();

            gameClient.PerceptionUpdated += p =>
            {
                lastPerception = p;
                if (!tcsFirstPerception.Task.IsCompleted)
                {
                    tcsFirstPerception.TrySetResult(p);
                }
                else if (!tcsSecondPerception.Task.IsCompleted)
                {
                    tcsSecondPerception.TrySetResult(p);
                }
            };

            Console.WriteLine("[SelfTest] Connecting to server...");
            
            // Retry connection with exponential backoff
            var maxRetries = 5;
            var baseDelay = 1000; // 1 second
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await gameClient.ConnectAsync();
                    Console.WriteLine("[SelfTest] Connected successfully");
                    break;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = baseDelay * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"[SelfTest] Connection attempt {attempt} failed: {ex.Message}");
                    Console.WriteLine($"[SelfTest] Retrying in {delay}ms...");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SelfTest] Final connection attempt failed: {ex.Message}");
                    File.WriteAllText(Path.Combine(artifactsDir, "error.txt"), $"Failed to connect to server after {maxRetries} attempts: {ex.Message}");
                    return 1;
                }
            }
            
            Console.WriteLine("[SelfTest] Connected, waiting for first perception...");

            // Wait for first perception with timeout
            var firstTimeout = Task.Delay(5000);
            var firstCompleted = await Task.WhenAny(tcsFirstPerception.Task, firstTimeout);
            if (firstCompleted == firstTimeout)
            {
                Console.WriteLine("[SelfTest] ERROR: Timeout waiting for first perception");
                File.WriteAllText(Path.Combine(artifactsDir, "error.txt"), "Timeout waiting for first perception from server");
                return 1;
            }
            var first = await tcsFirstPerception.Task;
            Console.WriteLine("[SelfTest] Got first perception");
            File.WriteAllText(Path.Combine(artifactsDir, "first_perception.txt"), $"Visuals: {first.Visuals.Count}\nPlayerLocation: {first.PlayerLocation.X},{first.PlayerLocation.Y},{first.PlayerLocation.Z}\n");
            mapView.Perception = first;
            mapView.WorldLocation = first.PlayerLocation;
            widgetManager.UpdateFromPerception(first);

            // Render initial frame
            Console.WriteLine($"[SelfTest] First perception has {first.Visuals.Count} visuals");
            if (first.Visuals.Count > 0)
            {
                var sampleKeys = first.Visuals.Keys.Take(5).ToArray();
                File.WriteAllText(Path.Combine(artifactsDir, "first_visual_keys.txt"), 
                    $"Visual keys (sample of 5): {string.Join(", ", sampleKeys)}\n" +
                    $"VisibleBounds: {first.VisibleBounds.X},{first.VisibleBounds.Y},{first.VisibleBounds.Width},{first.VisibleBounds.Height}\n" +
                    $"PlayerHeading: {first.PlayerHeading}\n" +
                    $"MapView screen pos: {mapView.ScreenPosition.X},{mapView.ScreenPosition.Y}, size: {mapView.Size.Width}x{mapView.Size.Height}\n");
            }
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

            // Small delay to ensure rendering completes
            await Task.Delay(100);
            Console.Out.Flush();

            // Snapshot before (characters)
            var beforeLines = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            File.WriteAllLines(Path.Combine(artifactsDir, "before.txt"), beforeLines);
            // Snapshot before (attributes heatmap)
            var defAttrBefore = ConsoleSnapshotter.GetCurrentAttributes();
            var beforeAttrs = ConsoleSnapshotter.CaptureAttrRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            var beforeAttrHeatmap = ConsoleSnapshotter.GenerateAttrHeatmap(beforeAttrs, defAttrBefore);
            File.WriteAllLines(Path.Combine(artifactsDir, "colors_before.txt"), beforeAttrHeatmap);
            // Extended capture to include info lines below the map
            var beforeExt = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height + 6);
            File.WriteAllLines(Path.Combine(artifactsDir, "before_ext.txt"), beforeExt);

            // Move down once
            Console.WriteLine("[SelfTest] Moving player down...");
            await gameClient.MovePlayerAsync(RelativeDirection.Backward, 1);

            // Wait for second perception update (with timeout)
            Console.WriteLine("[SelfTest] Waiting for second perception...");
            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcsSecondPerception.Task, timeoutTask);
            PerceptionDto? after = null;
            if (completedTask == tcsSecondPerception.Task)
            {
                after = await tcsSecondPerception.Task;
                Console.WriteLine("[SelfTest] Got second perception");
            }
            else
            {
                // Timeout - use last known perception or fallback to first
                after = lastPerception ?? first;
                Console.WriteLine("[SelfTest] Timeout - using last perception");
                File.WriteAllText(Path.Combine(artifactsDir, "timeout_warning.txt"), "Second perception update timed out\n");
            }

            // Small delay to ensure rendering completes
            await Task.Delay(100);

            mapView.Perception = after;
            mapView.WorldLocation = after.PlayerLocation;
            widgetManager.UpdateFromPerception(after);

            Console.WriteLine($"[SelfTest] Second perception has {after.Visuals.Count} visuals");
            File.WriteAllText(Path.Combine(artifactsDir, "second_perception.txt"), $"Visuals: {after.Visuals.Count}\nPlayerLocation: {after.PlayerLocation.X},{after.PlayerLocation.Y},{after.PlayerLocation.Z}\n");
            mapView.Clear(clearFrame: true);
            mapView.DrawFrame();
            mapView.DrawContents();
            state.Perception = after;
            renderer.RenderFrame(state);

            // Longer delay and flush to ensure all console writes complete
            await Task.Delay(500);
            Console.Out.Flush();

            var afterLines = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            File.WriteAllLines(Path.Combine(artifactsDir, "after.txt"), afterLines);
            // Snapshot after (attributes heatmap)
            var defAttrAfter = ConsoleSnapshotter.GetCurrentAttributes();
            var afterAttrs = ConsoleSnapshotter.CaptureAttrRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height);
            var afterAttrHeatmap = ConsoleSnapshotter.GenerateAttrHeatmap(afterAttrs, defAttrAfter);
            File.WriteAllLines(Path.Combine(artifactsDir, "colors_after.txt"), afterAttrHeatmap);
            // Extended capture
            var afterExt = ConsoleSnapshotter.CaptureRect(mapView.ScreenPosition.X, mapView.ScreenPosition.Y, mapView.Size.Width, mapView.Size.Height + 6);
            File.WriteAllLines(Path.Combine(artifactsDir, "after_ext.txt"), afterExt);

            // Diff heatmap (chars + attributes)
            var diffHeatmap = new string[mapView.Size.Height];
            int changedCells = 0;
            for (int y = 0; y < mapView.Size.Height; y++)
            {
                var row = new char[mapView.Size.Width];
                for (int x = 0; x < mapView.Size.Width; x++)
                {
                    var chBefore = y < beforeLines.Length && x < beforeLines[y].Length ? beforeLines[y][x] : ' ';
                    var chAfter = y < afterLines.Length && x < afterLines[y].Length ? afterLines[y][x] : ' ';
                    var attrBefore = beforeAttrs[y, x];
                    var attrAfter = afterAttrs[y, x];
                    bool chDiff = chBefore != chAfter;
                    bool attrDiff = attrBefore != attrAfter;
                    row[x] = chDiff && attrDiff ? 'B' : (chDiff ? 'C' : (attrDiff ? 'A' : '.'));
                    if (row[x] != '.') changedCells++;
                }
                diffHeatmap[y] = new string(row);
            }
            File.WriteAllLines(Path.Combine(artifactsDir, "diff_colors.txt"), diffHeatmap);
            File.WriteAllText(Path.Combine(artifactsDir, "diff_stats.txt"), $"changedCells={changedCells}\n");

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
            catch (Exception ex)
            {
                // Ensure artifacts directory exists
                try
                {
                    Directory.CreateDirectory(artifactsDir);
                    var errorMsg = $"Unhandled exception: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $"\n\nInner exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                    }
                    File.WriteAllText(Path.Combine(artifactsDir, "error.txt"), errorMsg);
                    Console.WriteLine($"[SelfTest] ERROR: {ex.Message}");
                    Console.WriteLine($"[SelfTest] Error details written to: {Path.Combine(artifactsDir, "error.txt")}");
                }
                catch
                {
                    // If we can't write error file, at least log to console
                    Console.WriteLine($"[SelfTest] FATAL: Unhandled exception: {ex.Message}");
                    Console.WriteLine($"[SelfTest] Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
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
            bool notBlank = spaceRatio < 0.99; // at least 1% non-space (adjusted to account for map positioning and widget areas)

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



