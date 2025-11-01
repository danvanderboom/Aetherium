using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Management;

namespace Aetherctl.SignalR
{
    /// <summary>
    /// SignalR client for connecting to Aetherium ManagementHub.
    /// </summary>
    public class ManagementClient : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly string _baseUrl;
        private readonly Func<Task<string>> _tokenProvider;

        public ManagementClient(string baseUrl, Func<Task<string>> tokenProvider)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Connects to the ManagementHub.
        /// </summary>
        public async Task ConnectAsync()
        {
            if (_connection != null)
                return;

            _connection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/managementHub", options =>
                {
                    options.AccessTokenProvider = async () => await _tokenProvider();
                })
                .WithAutomaticReconnect()
                .Build();

            await _connection.StartAsync();
        }

        /// <summary>
        /// Tests connection with a ping.
        /// </summary>
        public async Task<string> PingAsync()
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<string>("Ping");
        }

        /// <summary>
        /// Gets server information.
        /// </summary>
        public async Task<Dictionary<string, object>> GetServerInfoAsync()
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<Dictionary<string, object>>("GetServerInfo");
        }

        /// <summary>
        /// Lists all worlds.
        /// </summary>
        public async Task<List<WorldInfo>> ListWorldsAsync()
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<List<WorldInfo>>("ListWorlds");
        }

        /// <summary>
        /// Gets detailed information about a specific world.
        /// </summary>
        public async Task<WorldInfo?> GetWorldInfoAsync(string worldId)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<WorldInfo?>("GetWorldInfo", worldId);
        }

        /// <summary>
        /// Creates a new world. Requires Admin role.
        /// </summary>
        public async Task<string> CreateWorldAsync(CreateWorldRequest request)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<string>("CreateWorld", request);
        }

        /// <summary>
        /// Pauses a world. Requires Admin role.
        /// </summary>
        public async Task<OperationResult> PauseWorldAsync(string worldId)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<OperationResult>("PauseWorld", worldId);
        }

        /// <summary>
        /// Resumes a paused world. Requires Admin role.
        /// </summary>
        public async Task<OperationResult> ResumeWorldAsync(string worldId)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<OperationResult>("ResumeWorld", worldId);
        }

        /// <summary>
        /// Shuts down a world. Requires Admin role.
        /// </summary>
        public async Task<OperationResult> ShutdownAsync(string worldId)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<OperationResult>("Shutdown", worldId);
        }

        private void EnsureConnected()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }
}

