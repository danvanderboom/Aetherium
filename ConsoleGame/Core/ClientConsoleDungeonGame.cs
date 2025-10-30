using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using ConsoleGame.Client;
using ConsoleGame.Views;
using ConsoleGameModel;

namespace ConsoleGame.Core
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
    }
}

