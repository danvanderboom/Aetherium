using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aetherium.Client
{
    public class GameClient
    {
        private HubConnection? connection;
        private readonly string serverUrl;
        
        public event Action<PerceptionDto>? PerceptionUpdated;
        public event Action<GameStateDto>? GameStateReceived;
        public event Action? Connected;
        public event Action? Disconnected;

        public bool IsConnected => connection?.State == HubConnectionState.Connected;

        public GameClient(string serverUrl = "http://localhost:5000/gamehub")
        {
            this.serverUrl = serverUrl;
        }

        public async Task ConnectAsync()
        {
            connection = new HubConnectionBuilder()
                .WithUrl(serverUrl)
                .WithAutomaticReconnect()
                .Build();

            connection.On<PerceptionDto>("ReceivePerceptionUpdate", (perception) =>
            {
                PerceptionUpdated?.Invoke(perception);
            });

            connection.On<GameStateDto>("ReceiveGameState", (gameState) =>
            {
                GameStateReceived?.Invoke(gameState);
            });

            connection.Closed += async (error) =>
            {
                Disconnected?.Invoke();
                await Task.Delay(new Random().Next(0, 5) * 1000);
                try
                {
                    await connection.StartAsync();
                }
                catch
                {
                    // Will retry automatically due to WithAutomaticReconnect
                }
            };

            connection.Reconnected += (connectionId) =>
            {
                Connected?.Invoke();
                return Task.CompletedTask;
            };

            await connection.StartAsync();
            Connected?.Invoke();
            Console.WriteLine($"Connected to server at {serverUrl}");
        }

        public async Task MovePlayerAsync(RelativeDirection direction, int distance)
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("MovePlayer", direction, distance);
        }

        public async Task RotatePlayerAsync(bool clockwise)
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("RotatePlayer", clockwise);
        }

        public async Task RotatePlayerDegreesAsync(int degrees)
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("RotatePlayerDegrees", degrees);
        }

        public async Task ToggleDirectionalVisionAsync()
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("ToggleDirectionalVision");
        }

        public async Task ChangeLevelAsync(int deltaZ)
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("ChangeLevel", deltaZ);
        }

        public async Task JumpToRandomLocationAsync()
        {
            if (connection == null || !IsConnected)
                return;

            await connection.InvokeAsync("JumpToRandomLocation");
        }

        public async Task<InteractionResultDto?> PickupAsync(string targetEntityId)
        {
            if (connection == null || !IsConnected)
                return null;
            return await connection.InvokeAsync<InteractionResultDto>("Pickup", targetEntityId);
        }

        public async Task<InteractionResultDto?> DropAsync(string itemEntityId)
        {
            if (connection == null || !IsConnected)
                return null;
            return await connection.InvokeAsync<InteractionResultDto>("Drop", itemEntityId);
        }

        public async Task<InteractionResultDto?> UseAsync(string itemEntityId, string onEntityId, string? usageId = null)
        {
            if (connection == null || !IsConnected)
                return null;
            return await connection.InvokeAsync<InteractionResultDto>("Use", itemEntityId, onEntityId, usageId);
        }

        public async Task<InteractionResultDto?> OpenAsync(string targetEntityId)
        {
            if (connection == null || !IsConnected)
                return null;
            return await connection.InvokeAsync<InteractionResultDto>("Open", targetEntityId);
        }

        public async Task<InteractionResultDto?> CloseAsync(string targetEntityId)
        {
            if (connection == null || !IsConnected)
                return null;
            return await connection.InvokeAsync<InteractionResultDto>("Close", targetEntityId);
        }

        public async Task SetLightingModeAsync(LightingMode mode)
        {
            if (connection == null || !IsConnected)
                return;
            await connection.InvokeAsync("SetLightingMode", mode);
        }

        public async Task SetVisionModeAsync(VisionMode mode)
        {
            if (connection == null || !IsConnected)
                return;
            await connection.InvokeAsync("SetVisionMode", mode);
        }
        
        // ============================================================
        // Unified Tool Execution API (New)
        // ============================================================
        
        /// <summary>
        /// Executes a tool by ID with specified arguments (new unified API).
        /// </summary>
        public async Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, Dictionary<string, object> args)
        {
            if (connection == null || !IsConnected)
                return new ToolExecutionResultDto { Success = false, Message = "Not connected" };
            
            return await connection.InvokeAsync<ToolExecutionResultDto>("ExecuteTool", toolId, args);
        }
        
        /// <summary>
        /// Lists all available tools for the current player.
        /// </summary>
        public async Task<List<ToolInfoDto>> GetAvailableToolsAsync()
        {
            if (connection == null || !IsConnected)
                return new List<ToolInfoDto>();
            
            return await connection.InvokeAsync<List<ToolInfoDto>>("ListAvailableTools");
        }

        public async Task DisconnectAsync()
        {
            if (connection != null)
            {
                await connection.StopAsync();
                await connection.DisposeAsync();
            }
        }
    }
}


