using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Client;
using Aetherium.Views;
using Aetherium.Monitoring;
using Aetherium.Model;

namespace Aetherium.Core
{
    public class ClientConsoleDungeonGame
    {
        private GameClient gameClient;
        private ClientConsoleMapView mapView;
        private List<ConsoleView> views;

        private ConsoleColor oldBackgroundColor;
        private ConsoleColor oldForegroundColor;

        private PerceptionDto? currentPerception;
        private GameStateDto? gameState;

        private bool connected = false;

        public ClientConsoleDungeonGame(string serverUrl = "http://localhost:5000/gamehub")
        {
            gameClient = new GameClient(serverUrl);
            views = new List<ConsoleView>();

            mapView = new ClientConsoleMapView
            {
                ScreenPosition = new Point(3, 2),
                Size = new Size(42, 22),
                BackgroundColor = ConsoleColor.Black,
                HasFrame = true,
                FrameBackgroundColor = ConsoleColor.DarkGray,
                FrameForegroundColor = ConsoleColor.Black
            };

            views.Add(mapView);

            // Subscribe to game client events
            gameClient.PerceptionUpdated += OnPerceptionUpdated;
            gameClient.GameStateReceived += OnGameStateReceived;
            gameClient.Connected += OnConnected;
            gameClient.Disconnected += OnDisconnected;
        }

        private void OnPerceptionUpdated(PerceptionDto perception)
        {
            currentPerception = perception;
            mapView.Perception = perception;
            mapView.WorldLocation = perception.PlayerLocation;
            DisplayViewContents();

            // Broadcast to monitoring clients if enabled
            if (MapFrameMonitor.Instance.IsRunning)
            {
                var asciiMap = mapView.CaptureRenderedFrame();
                _ = MapFrameMonitor.Instance.BroadcastFrameAsync(perception, asciiMap);
            }
        }

        private void OnGameStateReceived(GameStateDto state)
        {
            gameState = state;
            Console.WriteLine($"Game state received. Player ID: {state.PlayerId}");
        }

        private void OnConnected()
        {
            connected = true;
            Console.WriteLine("Connected to server!");
        }

        private void OnDisconnected()
        {
            connected = false;
            Console.WriteLine("Disconnected from server. Attempting to reconnect...");
        }

        public async Task Run()
        {
            oldBackgroundColor = Console.BackgroundColor;
            oldForegroundColor = Console.ForegroundColor;

            try
            {
                if (!Console.IsOutputRedirected)
                    Console.CursorVisible = false;
            }
            catch (System.IO.IOException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
            Console.OutputEncoding = Encoding.Unicode;

            Clear(ConsoleColor.Black);

            Console.WriteLine("Connecting to server...");

            try
            {
                await gameClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to server: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Wait for initial perception
            await Task.Delay(500);

            Clear(ConsoleColor.Black);
            DisplayViews();

            while (true)
            {
                while (!Console.KeyAvailable)
                {
                    await Task.Delay(50);
                }

                var keyInfo = Console.ReadKey(true);
                await HandleCommand(keyInfo);
            }
        }

        private async Task HandleCommand(ConsoleKeyInfo keyInfo)
        {
            if (!connected)
                return;

            try
            {
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        await gameClient.MovePlayerAsync(RelativeDirection.Forward, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.DownArrow:
                        await gameClient.MovePlayerAsync(RelativeDirection.Backward, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.LeftArrow:
                        await gameClient.MovePlayerAsync(RelativeDirection.Left, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.RightArrow:
                        await gameClient.MovePlayerAsync(RelativeDirection.Right, Console.CapsLock ? 10 : 1);
                        break;
                    case ConsoleKey.Z:
                        await gameClient.RotatePlayerAsync(false); // counter-clockwise
                        break;
                    case ConsoleKey.X:
                        await gameClient.RotatePlayerAsync(true); // clockwise
                        break;
                    case ConsoleKey.U:
                        await gameClient.ChangeLevelAsync(Console.CapsLock ? +10 : +1);
                        break;
                    case ConsoleKey.D:
                        await gameClient.ChangeLevelAsync(Console.CapsLock ? -10 : -1);
                        break;
                    case ConsoleKey.D0: // digit zero
                        // Reset to level 0 - client doesn't know absolute Z, so this command is removed
                        // The client should not know its absolute Z coordinate
                        // Player can use U/D keys to change levels relative to current position
                        break;
                    case ConsoleKey.J:
                        await gameClient.JumpToRandomLocationAsync();
                        break;
                    case ConsoleKey.OemComma: // ',' pickup
                        await HandlePickup();
                        break;
                    case ConsoleKey.OemPeriod: // '.' drop
                        await HandleDrop();
                        break;
                    case ConsoleKey.E:
                    case ConsoleKey.I: // Unified interact command
                        await HandleInteract();
                        break;
                    case ConsoleKey.O:
                        await HandleOpen();
                        break;
                    case ConsoleKey.C:
                        if (keyInfo.Modifiers == ConsoleModifiers.Control)
                        {
                            await HandleClose();
                        }
                        break;
                    case ConsoleKey.M:
                        // Toggle grid coloring
                        if (mapView.GridColoring == null)
                        {
                            var choice = Console.ReadKey(true).KeyChar.ToString();
                            if (int.TryParse(choice, out var gridIndex))
                            {
                                if (gridIndex == 0)
                                    mapView.GridColoring = new ConsoleColor[2, 2]
                                    {
                                        { ConsoleColor.Red, ConsoleColor.Blue },
                                        { ConsoleColor.Blue, ConsoleColor.White }
                                    };
                                else if (gridIndex == 1)
                                    mapView.GridColoring = new ConsoleColor[3, 3]
                                    {
                                        { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Yellow },
                                        { ConsoleColor.Blue, ConsoleColor.Blue, ConsoleColor.Yellow },
                                        { ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Blue }
                                    };
                                else if (gridIndex == 2)
                                    mapView.GridColoring = new ConsoleColor[3, 3]
                                    {
                                        { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Yellow },
                                        { ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Yellow },
                                        { ConsoleColor.Blue, ConsoleColor.Yellow, ConsoleColor.Cyan }
                                    };
                            }
                        }
                        else
                        {
                            mapView.GridColoring = null;
                        }
                        DisplayViewContents();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command: {ex.Message}");
            }
        }

        public void DisplayViews()
        {
            foreach (var view in views)
            {
                if (view.HasFrame)
                    view.DrawFrame();

                view.DrawContents();
            }
        }

        public void DisplayViewContents()
        {
            foreach (var view in views)
                view.DrawContents();
        }

        void Clear(ConsoleColor? backgroundColor = null)
        {
            if (backgroundColor.HasValue)
            {
                oldBackgroundColor = backgroundColor.Value;
                Console.BackgroundColor = backgroundColor.Value;
            }

            Console.Clear();
        }

        private async Task HandlePickup()
        {
            if (currentPerception?.VisibleItems == null || !currentPerception.VisibleItems.Any())
            {
                Console.WriteLine("No items visible to pick up. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            // For simplicity, pick up the first visible item at player location
            // In a full UI, you'd show a menu to select which item
            var targetId = currentPerception.VisibleItems.First().Id;
            var result = await gameClient.PickupAsync(targetId);
            if (result != null)
            {
                var msg = result.Success ? "Picked up item!" : $"Failed: {result.Reason}";
                Console.WriteLine(msg);
                await Task.Delay(500);
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private async Task HandleDrop()
        {
            if (currentPerception?.Inventory == null || !currentPerception.Inventory.Items.Any())
            {
                Console.WriteLine("Inventory is empty. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            // Drop the last item in inventory
            var itemId = currentPerception.Inventory.Items.Last().Id;
            var result = await gameClient.DropAsync(itemId);
            if (result != null)
            {
                var msg = result.Success ? "Dropped item!" : $"Failed: {result.Reason}";
                Console.WriteLine(msg);
                await Task.Delay(500);
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private async Task HandleUse()
        {
            if (currentPerception?.Inventory == null || !currentPerception.Inventory.Items.Any())
            {
                Console.WriteLine("No items to use. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            // Use first item in inventory on first affordance that requires a key
            var item = currentPerception.Inventory.Items.First();
            var affordance = currentPerception.Affordances?.FirstOrDefault(a => a.Action == "use" && a.RequiresKeyId != null);
            if (affordance == null)
            {
                Console.WriteLine("No targets requiring keys found. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            var result = await gameClient.UseAsync(item.Id, affordance.TargetId ?? "");
            if (result != null)
            {
                var msg = result.Success ? "Used item!" : $"Failed: {result.Reason}";
                Console.WriteLine(msg);
                await Task.Delay(500);
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private async Task HandleOpen()
        {
            var affordance = currentPerception?.Affordances?.FirstOrDefault(a => a.Action == "open");
            if (affordance == null)
            {
                Console.WriteLine("Nothing to open. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            var result = await gameClient.OpenAsync(affordance.TargetId ?? "");
            if (result != null)
            {
                var msg = result.Success ? "Opened!" : $"Failed: {result.Reason}";
                Console.WriteLine(msg);
                await Task.Delay(500);
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private async Task HandleClose()
        {
            var affordance = currentPerception?.Affordances?.FirstOrDefault(a => a.Action == "close");
            if (affordance == null)
            {
                Console.WriteLine("Nothing to close. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            var result = await gameClient.CloseAsync(affordance.TargetId ?? "");
            if (result != null)
            {
                var msg = result.Success ? "Closed!" : $"Failed: {result.Reason}";
                Console.WriteLine(msg);
                await Task.Delay(500);
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private async Task HandleInteract()
        {
            // Show available actions from affordances
            if (currentPerception?.Affordances == null || !currentPerception.Affordances.Any())
            {
                Console.WriteLine("No actions available here. Press any key to continue...");
                Console.ReadKey(true);
                return;
            }

            // Group affordances by action type for display
            var grouped = currentPerception.Affordances
                .GroupBy(a => a.Action)
                .ToList();

            Console.Clear();
            Console.WriteLine("=== Available Actions ===");
            Console.WriteLine();

            int index = 1;
            var actionMap = new Dictionary<int, AffordanceDto>();

            foreach (var group in grouped)
            {
                foreach (var aff in group)
                {
                    var description = BuildAffordanceDescription(aff);
                    Console.WriteLine($"{index}. {aff.Action.ToUpperInvariant()}: {description}");
                    actionMap[index] = aff;
                    index++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{index}. Cancel");
            Console.WriteLine();
            Console.Write($"Select action (1-{index}): ");

            try
            {
                var input = Console.ReadLine();
                if (int.TryParse(input, out var choice))
                {
                    if (choice == index)
                    {
                        // Cancel
                        Clear(ConsoleColor.Black);
                        DisplayViews();
                        return;
                    }

                    if (actionMap.TryGetValue(choice, out var selectedAffordance))
                    {
                        await ExecuteAffordance(selectedAffordance);
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice. Press any key to continue...");
                        Console.ReadKey(true);
                        Clear(ConsoleColor.Black);
                        DisplayViews();
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input. Press any key to continue...");
                    Console.ReadKey(true);
                    Clear(ConsoleColor.Black);
                    DisplayViews();
                }
            }
            catch
            {
                // Fallback if input fails
                Clear(ConsoleColor.Black);
                DisplayViews();
            }
        }

        private string BuildAffordanceDescription(AffordanceDto aff)
        {
            var item = currentPerception?.Inventory?.Items.FirstOrDefault(i => i.Id == aff.TargetId);
            var visibleItem = currentPerception?.VisibleItems?.FirstOrDefault(i => i.Id == aff.TargetId);
            var itemLabel = item?.Label ?? visibleItem?.Label ?? $"Entity {aff.TargetId?.Substring(0, Math.Min(8, aff.TargetId?.Length ?? 0))}";

            switch (aff.Action.ToLowerInvariant())
            {
                case "pickup":
                    return $"Pick up {itemLabel}";
                case "drop":
                    return $"Drop {itemLabel}";
                case "use":
                    var requiresKey = !string.IsNullOrEmpty(aff.RequiresKeyId) ? $" (requires {aff.RequiresKeyId} key)" : "";
                    return $"Use item on {itemLabel}{requiresKey}";
                case "open":
                    return $"Open {itemLabel}";
                case "close":
                    return $"Close {itemLabel}";
                default:
                    return $"{aff.Action} {itemLabel}";
            }
        }

        private async Task ExecuteAffordance(AffordanceDto aff)
        {
            InteractionResultDto? result = null;
            string msg = "";

            switch (aff.Action.ToLowerInvariant())
            {
                case "pickup":
                    result = await gameClient.PickupAsync(aff.TargetId ?? "");
                    msg = result?.Success == true ? "Picked up!" : $"Failed: {result?.Reason ?? "Unknown error"}";
                    break;
                case "drop":
                    result = await gameClient.DropAsync(aff.TargetId ?? "");
                    msg = result?.Success == true ? "Dropped!" : $"Failed: {result?.Reason ?? "Unknown error"}";
                    break;
                case "use":
                    // For use, we need an item from inventory and a target
                    if (currentPerception?.Inventory?.Items != null && currentPerception.Inventory.Items.Any())
                    {
                        // Use ItemId from affordance if available, otherwise find matching item
                        var item = !string.IsNullOrEmpty(aff.ItemId)
                            ? currentPerception.Inventory.Items.FirstOrDefault(i => i.Id == aff.ItemId)
                            : currentPerception.Inventory.Items.FirstOrDefault(i => 
                                !string.IsNullOrEmpty(i.KeyId) && i.KeyId == aff.RequiresKeyId);
                        
                        if (item == null)
                            item = currentPerception.Inventory.Items.First();
                        
                        // Check for proactive UsageOptions (2+ options)
                        string? selectedUsageId = null;
                        if (aff.UsageOptions != null && aff.UsageOptions.Count > 1)
                        {
                            // Present choice to user
                            Console.WriteLine("\nMultiple usage options available:");
                            for (int i = 0; i < aff.UsageOptions.Count; i++)
                            {
                                Console.WriteLine($"{i + 1}. {aff.UsageOptions[i].Label} - {aff.UsageOptions[i].UsageId}");
                            }
                            Console.Write("Select option (1-" + aff.UsageOptions.Count + "): ");
                            var input = Console.ReadLine();
                            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= aff.UsageOptions.Count)
                            {
                                selectedUsageId = aff.UsageOptions[choice - 1].UsageId;
                            }
                            else
                            {
                                msg = "Invalid selection";
                                break;
                            }
                        }
                        else if (aff.UsageOptions != null && aff.UsageOptions.Count == 1)
                        {
                            // Auto-select single option
                            selectedUsageId = aff.UsageOptions[0].UsageId;
                        }
                        
                        result = await gameClient.UseAsync(item.Id, aff.TargetId ?? "", selectedUsageId);
                        
                        // Handle reactive disambiguation (options returned from server)
                        if (result?.Options != null && result.Options.Count > 0)
                        {
                            Console.WriteLine("\nMultiple usage options available:");
                            for (int i = 0; i < result.Options.Count; i++)
                            {
                                Console.WriteLine($"{i + 1}. {result.Options[i].Label} - {result.Options[i].UsageId}");
                            }
                            Console.Write("Select option (1-" + result.Options.Count + "): ");
                            var input = Console.ReadLine();
                            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= result.Options.Count)
                            {
                                selectedUsageId = result.Options[choice - 1].UsageId;
                                // Retry with selected usageId
                                result = await gameClient.UseAsync(item.Id, aff.TargetId ?? "", selectedUsageId);
                            }
                            else
                            {
                                msg = "Invalid selection";
                                break;
                            }
                        }
                        
                        msg = result?.Success == true ? "Used!" : $"Failed: {result?.Reason ?? "Unknown error"}";
                    }
                    else
                    {
                        msg = "No items in inventory to use";
                    }
                    break;
                case "open":
                    result = await gameClient.OpenAsync(aff.TargetId ?? "");
                    msg = result?.Success == true ? "Opened!" : $"Failed: {result?.Reason ?? "Unknown error"}";
                    break;
                case "close":
                    result = await gameClient.CloseAsync(aff.TargetId ?? "");
                    msg = result?.Success == true ? "Closed!" : $"Failed: {result?.Reason ?? "Unknown error"}";
                    break;
            }

            Console.WriteLine(msg);
            await Task.Delay(1000);
            Clear(ConsoleColor.Black);
            DisplayViews();
        }
    }
}


