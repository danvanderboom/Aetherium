using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Client;
using Aetherium.Views;
using Aetherium.Monitoring;
using Aetherium.Rendering;
using Aetherium.Rendering.Widgets;
using Aetherium.Rendering.Themes;
using Aetherium.Audio;
using Aetherium.Model;

namespace Aetherium.Core
{
    /// <summary>
    /// Main client game class integrating rendering abstraction, widgets, and audio
    /// </summary>
    public class ClientConsoleDungeonGameNew
    {
        private readonly GameClient gameClient;
        private readonly IGameRenderer renderer;
        private readonly WidgetManager widgetManager;
        private readonly IAudioSystem audioSystem;
        private readonly AudioDirector audioDirector;
        private readonly ClientConsoleMapView mapView;

        private PerceptionDto? currentPerception;
        private GameStateDto? gameState;
        private ThemeConfig currentTheme;
        private bool connected = false;
        private string? statusMessage;

        private CompassWidget? compassWidget;
        private InventoryWidget? inventoryWidget;

        public ClientConsoleDungeonGameNew(
            string serverUrl = "http://localhost:5000/gamehub",
            IGameRenderer? renderer = null,
            IAudioSystem? audioSystem = null,
            string themeName = "zen")
        {
            gameClient = new GameClient(serverUrl);
            currentTheme = BuiltInThemes.GetByName(themeName);
            
            this.renderer = renderer ?? new SpectreConsoleRenderer();
            this.audioSystem = audioSystem ?? CreateAudioSystem();
            this.audioDirector = new AudioDirector(this.audioSystem);
            
            widgetManager = new WidgetManager();
            
            // Create map view (still using existing view for map rendering)
            mapView = new ClientConsoleMapView
            {
                ScreenPosition = new Point(3, 2),
                Size = new Size(42, 22),
                BackgroundColor = ConsoleColor.Black,
                HasFrame = true,
                FrameBackgroundColor = ConsoleColor.DarkGray,
                FrameForegroundColor = ConsoleColor.Black
            };

            // Create and register widgets
            compassWidget = new CompassWidget(currentTheme);
            widgetManager.RegisterWidget(compassWidget);

            inventoryWidget = new InventoryWidget(currentTheme);
            widgetManager.RegisterWidget(inventoryWidget);

            // Subscribe to game client events
            gameClient.PerceptionUpdated += OnPerceptionUpdated;
            gameClient.GameStateReceived += OnGameStateReceived;
            gameClient.Connected += OnConnected;
            gameClient.Disconnected += OnDisconnected;
        }

        private IAudioSystem CreateAudioSystem()
        {
            try
            {
                var config = new AudioConfig
                {
                    Enabled = true,
                    MusicVolume = 0.5f,
                    EffectsVolume = 0.7f,
                    DefaultMusicTrack = "mellow-guitar-loop",
                    AssetPath = "Assets/Audio"
                };

                return new NAudioSystem(config);
            }
            catch
            {
                // If NAudio fails to initialize, use null audio system
                return new NullAudioSystem();
            }
        }

        private void OnPerceptionUpdated(PerceptionDto perception)
        {
            currentPerception = perception;
            mapView.Perception = perception;
            mapView.WorldLocation = perception.PlayerLocation;

            // Update widgets based on perception
            widgetManager.UpdateFromPerception(perception);
            compassWidget?.UpdateNavigationData(perception.NavigationData, 
                perception.IsDirectionalVision, perception.FieldOfViewDegrees);
            inventoryWidget?.UpdateInventoryData(perception.Inventory);

            // Update audio based on perception
            audioDirector.OnPerception(perception);

            // Render frame
            RenderCurrentState();

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
            statusMessage = $"Connected - Player ID: {state.PlayerId}";
        }

        private void OnConnected()
        {
            connected = true;
            statusMessage = "Connected to server!";
            
            // Start background music
            audioSystem.PlayBackgroundMusic(audioSystem is NAudioSystem ? "mellow-guitar-loop" : "", loop: true);
        }

        private void OnDisconnected()
        {
            connected = false;
            statusMessage = "Disconnected. Reconnecting...";
        }

        public async Task Run()
        {
            renderer.Initialize();

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
                renderer.Shutdown();
                return;
            }

            // Wait for initial perception
            await Task.Delay(500);

            renderer.Clear();
            RenderCurrentState();

            // Main game loop
            while (true)
            {
                var keyInfo = await renderer.WaitForInputCommandAsync();
                await HandleCommand(keyInfo);
            }
        }

        private void RenderCurrentState()
        {
            // Build view state
            var viewState = new GameViewState
            {
                Perception = currentPerception,
                Widgets = widgetManager.GetAllWidgets(),
                Theme = currentTheme,
                IsConnected = connected,
                StatusMessage = statusMessage,
                Timestamp = DateTime.UtcNow
            };

            // For now, we render the map view directly (hybrid approach)
            // Clear the map region (including frame) to avoid ghosting from previous frames
            mapView.Clear(clearFrame: true);
            mapView.DrawFrame();
            mapView.DrawContents();

            // Render widgets using the renderer
            renderer.RenderFrame(viewState);
        }

        private async Task HandleCommand(ConsoleKeyInfo keyInfo)
        {
            if (!connected)
                return;

            try
            {
                switch (keyInfo.Key)
                {
                    // Movement
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        await gameClient.MovePlayerAsync(RelativeDirection.Forward, 1);
                        audioDirector.PlayFootstep();
                        break;
                    
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        await gameClient.MovePlayerAsync(RelativeDirection.Backward, 1);
                        audioDirector.PlayFootstep();
                        break;
                    
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        await gameClient.MovePlayerAsync(RelativeDirection.Left, 1);
                        audioDirector.PlayFootstep();
                        break;
                    
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        await gameClient.MovePlayerAsync(RelativeDirection.Right, 1);
                        audioDirector.PlayFootstep();
                        break;

                    // Rotation
                    case ConsoleKey.Q:
                        // Q: Rotate 15 degrees counter-clockwise (fine adjustment)
                        await gameClient.RotatePlayerDegreesAsync(-15);
                        break;
                    
                    case ConsoleKey.E:
                        // E: Rotate 15 degrees clockwise (fine adjustment)
                        await gameClient.RotatePlayerDegreesAsync(15);
                        break;

                    case ConsoleKey.Z:
                        // Z: Rotate 90 degrees counter-clockwise (sharp turn)
                        await gameClient.RotatePlayerDegreesAsync(-90);
                        break;

                    case ConsoleKey.C:
                        // C: Rotate 90 degrees clockwise (sharp turn)
                        await gameClient.RotatePlayerDegreesAsync(90);
                        break;

                    case ConsoleKey.T:
                        // T: Toggle directional vision mode (test feature)
                        await gameClient.ToggleDirectionalVisionAsync();
                        break;

                    // Level change
                    case ConsoleKey.PageUp:
                    case ConsoleKey.R:
                        await gameClient.ChangeLevelAsync(1);
                        break;
                    
                    case ConsoleKey.PageDown:
                    case ConsoleKey.F:
                        await gameClient.ChangeLevelAsync(-1);
                        break;

                    // Interactions
                    case ConsoleKey.G:
                        await HandlePickup();
                        break;
                    
                    case ConsoleKey.P:
                        await HandleDrop();
                        break;
                    
                    case ConsoleKey.O:
                        await HandleOpen();
                        break;
                    
                    case ConsoleKey.L:
                        // Changed from C to L to avoid conflict with rotation (C = Rotate clockwise)
                        await HandleClose();
                        break;

                    // UI Controls
                    case ConsoleKey.M:
                        // Toggle compass mode, or cycle the music track when Shift is held.
                        // (This logic was previously stranded as dead code after the Mode-4
                        // case's break, so M/Shift+M — advertised in the help panel — did
                        // nothing.)
                        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                        {
                            audioSystem.NextMusicTrack();
                            statusMessage = $"Music: {audioSystem.CurrentTrack ?? "None"}";
                        }
                        else if (compassWidget != null && compassWidget.IsVisible)
                        {
                            compassWidget.ToggleMode();
                            statusMessage = $"Compass mode: {compassWidget.Mode}";
                        }
                        break;

                    // Vision/Lighting Mode Switches (Number Keys 1-4)
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        // Mode 1: Normal Vision + Torch Lighting
                        await gameClient.SetLightingModeAsync(LightingMode.Torch);
                        await gameClient.SetVisionModeAsync(VisionMode.Normal);
                        statusMessage = "Mode 1: Normal Vision + Torch";
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        // Mode 2: Normal Vision + Sunlight
                        await gameClient.SetLightingModeAsync(LightingMode.Sunlight);
                        await gameClient.SetVisionModeAsync(VisionMode.Normal);
                        statusMessage = "Mode 2: Normal Vision + Sunlight";
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        // Mode 3: Infrared Vision + Torch (for comparison)
                        await gameClient.SetLightingModeAsync(LightingMode.Torch);
                        await gameClient.SetVisionModeAsync(VisionMode.Infrared);
                        statusMessage = "Mode 3: Infrared Vision + Torch";
                        break;

                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        // Mode 4: Infrared Vision + Sunlight
                        await gameClient.SetLightingModeAsync(LightingMode.Sunlight);
                        await gameClient.SetVisionModeAsync(VisionMode.Infrared);
                        statusMessage = "Mode 4: Infrared Vision + Sunlight";
                        break;

                    case ConsoleKey.N:
                        // Toggle music on/off
                        audioSystem.IsEnabled = !audioSystem.IsEnabled;
                        if (!audioSystem.IsEnabled)
                            audioSystem.StopBackgroundMusic();
                        else
                            audioSystem.PlayBackgroundMusic("mellow-guitar-loop", true);
                        statusMessage = $"Audio: {(audioSystem.IsEnabled ? "On" : "Off")}";
                        break;

                    case ConsoleKey.H:
                        // Changed from T to H to avoid conflict with directional vision toggle (T)
                        // Cycle themes
                        CycleTheme();
                        break;

                    // Teleport/Debug
                    case ConsoleKey.J:
                        await gameClient.JumpToRandomLocationAsync();
                        audioSystem.PlaySoundEffect("teleport");
                        break;

                    case ConsoleKey.Escape:
                        Cleanup();
                        Environment.Exit(0);
                        break;
                }

                RenderCurrentState();
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task HandlePickup()
        {
            if (currentPerception?.VisibleItems == null || !currentPerception.VisibleItems.Any())
            {
                statusMessage = "No items to pick up";
                return;
            }

            var targetId = currentPerception.VisibleItems.First().Id;
            var result = await gameClient.PickupAsync(targetId);
            if (result != null)
            {
                statusMessage = result.Success ? "Picked up item!" : $"Failed: {result.Reason}";
                if (result.Success)
                    audioSystem.PlaySoundEffect("item-pickup");
            }
        }

        private async Task HandleDrop()
        {
            if (currentPerception?.Inventory == null || !currentPerception.Inventory.Items.Any())
            {
                statusMessage = "No items to drop";
                return;
            }

            var itemId = currentPerception.Inventory.Items.First().Id;
            var result = await gameClient.DropAsync(itemId);
            if (result != null)
            {
                statusMessage = result.Success ? "Dropped item!" : $"Failed: {result.Reason}";
                if (result.Success)
                    audioSystem.PlaySoundEffect("item-drop");
            }
        }

        private async Task HandleOpen()
        {
            // Find door affordance
            var doorAffordance = currentPerception?.Affordances?.FirstOrDefault(a => a.Action == "open");
            if (doorAffordance != null)
            {
                var result = await gameClient.OpenAsync(doorAffordance.TargetId);
                if (result != null)
                {
                    statusMessage = result.Success ? "Opened door!" : $"Failed: {result.Reason}";
                    if (result.Success)
                        audioSystem.PlaySoundEffect("door-unlock");
                }
            }
            else
            {
                statusMessage = "No door to open";
            }
        }

        private async Task HandleClose()
        {
            var doorAffordance = currentPerception?.Affordances?.FirstOrDefault(a => a.Action == "close");
            if (doorAffordance != null)
            {
                var result = await gameClient.CloseAsync(doorAffordance.TargetId);
                if (result != null)
                {
                    statusMessage = result.Success ? "Closed door!" : $"Failed: {result.Reason}";
                    if (result.Success)
                        audioSystem.PlaySoundEffect("door-close");
                }
            }
            else
            {
                statusMessage = "No door to close";
            }
        }

        private void CycleTheme()
        {
            var themes = new[] { "zen", "cyberpunk", "halloween", "winter", "classic" };
            var currentIndex = Array.IndexOf(themes, currentTheme.Name.ToLower());
            var nextIndex = (currentIndex + 1) % themes.Length;
            
            currentTheme = BuiltInThemes.GetByName(themes[nextIndex]);
            compassWidget?.UpdateTheme(currentTheme);
            inventoryWidget?.UpdateTheme(currentTheme);
            
            statusMessage = $"Theme: {currentTheme.Name}";
        }

        private void Cleanup()
        {
            renderer.Shutdown();
            audioSystem?.Dispose();
            _ = gameClient.DisconnectAsync();
        }
    }
}


