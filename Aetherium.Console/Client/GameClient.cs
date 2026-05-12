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

        // ============================================================
        // Player verbs.
        //
        // As of phase 2d, the server's legacy per-verb hub methods
        // (MovePlayer, Pickup, Open, etc.) are removed. These client wrappers
        // now route through the unified ExecuteTool API. The CALLER surface
        // (this class's public methods) is unchanged — only the wire protocol
        // and the server-side dispatch path changed.
        // ============================================================

        public async Task MovePlayerAsync(RelativeDirection direction, int distance)
        {
            var dirString = direction switch
            {
                RelativeDirection.Forward => "FORWARD",
                RelativeDirection.Backward => "BACKWARD",
                RelativeDirection.Left => "LEFT",
                RelativeDirection.Right => "RIGHT",
                _ => "FORWARD",
            };
            await ExecuteToolAsync("move", new Dictionary<string, object>
            {
                ["direction"] = dirString,
                ["distance"] = distance,
            });
        }

        public async Task RotatePlayerAsync(bool clockwise)
        {
            await ExecuteToolAsync("rotate", new Dictionary<string, object>
            {
                ["clockwise"] = clockwise,
            });
        }

        public async Task RotatePlayerDegreesAsync(int degrees)
        {
            await ExecuteToolAsync("rotate", new Dictionary<string, object>
            {
                ["degrees"] = degrees,
            });
        }

        public async Task ToggleDirectionalVisionAsync()
        {
            await ExecuteToolAsync("toggledirectionalvision", new Dictionary<string, object>());
        }

        public async Task ChangeLevelAsync(int deltaZ)
        {
            await ExecuteToolAsync("changelevel", new Dictionary<string, object>
            {
                ["delta"] = deltaZ,
            });
        }

        public async Task JumpToRandomLocationAsync()
        {
            await ExecuteToolAsync("jumptolocation", new Dictionary<string, object>());
        }

        public async Task<InteractionResultDto?> PickupAsync(string targetEntityId)
        {
            var result = await ExecuteToolAsync("pickup", new Dictionary<string, object>
            {
                ["targetEntityId"] = targetEntityId,
            });
            return new InteractionResultDto { Success = result.Success, Reason = result.Message ?? string.Empty };
        }

        public async Task<InteractionResultDto?> DropAsync(string itemEntityId)
        {
            var result = await ExecuteToolAsync("drop", new Dictionary<string, object>
            {
                ["itemEntityId"] = itemEntityId,
            });
            return new InteractionResultDto { Success = result.Success, Reason = result.Message ?? string.Empty };
        }

        public async Task<InteractionResultDto?> UseAsync(string itemEntityId, string onEntityId, string? usageId = null)
        {
            var args = new Dictionary<string, object>
            {
                ["itemEntityId"] = itemEntityId,
                ["onEntityId"] = onEntityId,
            };
            if (!string.IsNullOrEmpty(usageId)) args["usageId"] = usageId;
            var result = await ExecuteToolAsync("use", args);
            return new InteractionResultDto { Success = result.Success, Reason = result.Message ?? string.Empty };
        }

        public async Task<InteractionResultDto?> OpenAsync(string targetEntityId)
        {
            var result = await ExecuteToolAsync("open", new Dictionary<string, object>
            {
                ["targetEntityId"] = targetEntityId,
            });
            return new InteractionResultDto { Success = result.Success, Reason = result.Message ?? string.Empty };
        }

        public async Task<InteractionResultDto?> CloseAsync(string targetEntityId)
        {
            var result = await ExecuteToolAsync("close", new Dictionary<string, object>
            {
                ["targetEntityId"] = targetEntityId,
            });
            return new InteractionResultDto { Success = result.Success, Reason = result.Message ?? string.Empty };
        }

        public async Task SetLightingModeAsync(LightingMode mode)
        {
            await ExecuteToolAsync("setlightingmode", new Dictionary<string, object>
            {
                ["mode"] = mode.ToString(),
            });
        }

        public async Task SetVisionModeAsync(VisionMode mode)
        {
            await ExecuteToolAsync("setvisionmode", new Dictionary<string, object>
            {
                ["mode"] = mode.ToString(),
            });
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


