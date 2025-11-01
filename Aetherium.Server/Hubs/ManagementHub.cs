using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Orleans;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Hubs
{
    /// <summary>
    /// SignalR hub for CLI management operations.
    /// Requires authentication via Azure AD B2C.
    /// </summary>
    [Authorize]
    public class ManagementHub : Hub
    {
        private readonly IClusterClient? _clusterClient;
        private readonly IGameManagementGrain? _managementGrain;

        public ManagementHub(IServiceProvider serviceProvider)
        {
            _clusterClient = serviceProvider.GetService(typeof(IClusterClient)) as IClusterClient;
            _managementGrain = _clusterClient?.GetGrain<IGameManagementGrain>("GLOBAL");
        }

        /// <summary>
        /// Ping endpoint to test connection.
        /// </summary>
        public Task<string> Ping()
        {
            return Task.FromResult("pong");
        }

        /// <summary>
        /// Gets server information (cluster ID, world count, etc.).
        /// </summary>
        public async Task<Dictionary<string, object>> GetServerInfo()
        {
            var info = new Dictionary<string, object>
            {
                ["clusterId"] = Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "dev",
                ["serviceId"] = Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ?? "Aetherium",
                ["timestamp"] = DateTime.UtcNow
            };

            if (_managementGrain != null)
            {
                try
                {
                    var worlds = await _managementGrain.ListWorldsAsync();
                    info["worldCount"] = worlds.Count;
                    info["activeWorlds"] = worlds.Count(w => w.State == WorldState.Active);
                    info["sessionCount"] = await _managementGrain.GetSessionCountAsync();
                }
                catch (Exception ex)
                {
                    info["error"] = ex.Message;
                }
            }
            else
            {
                info["worldCount"] = 0;
                info["activeWorlds"] = 0;
                info["sessionCount"] = 0;
                info["orleansStatus"] = "disabled";
            }

            return info;
        }

        /// <summary>
        /// Lists all worlds.
        /// </summary>
        public async Task<List<WorldInfo>> ListWorlds()
        {
            if (_managementGrain == null)
                return new List<WorldInfo>();

            try
            {
                return await _managementGrain.ListWorldsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error listing worlds: {ex.Message}");
                return new List<WorldInfo>();
            }
        }

        /// <summary>
        /// Gets detailed information about a specific world.
        /// </summary>
        public async Task<WorldInfo?> GetWorldInfo(string worldId)
        {
            if (_managementGrain == null)
                return null;

            try
            {
                return await _managementGrain.GetWorldInfoAsync(worldId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting world info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new world. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<string> CreateWorld(CreateWorldRequest request)
        {
            if (_managementGrain == null)
                throw new InvalidOperationException("Orleans is not available");

            try
            {
                return await _managementGrain.CreateWorldAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error creating world: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Pauses a world. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> PauseWorld(string worldId)
        {
            if (_managementGrain == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                return await _managementGrain.PauseWorldAsync(worldId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error pausing world: {ex.Message}");
                return OperationResult.Error($"Failed to pause world: {ex.Message}");
            }
        }

        /// <summary>
        /// Resumes a paused world. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> ResumeWorld(string worldId)
        {
            if (_managementGrain == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                return await _managementGrain.ResumeWorldAsync(worldId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error resuming world: {ex.Message}");
                return OperationResult.Error($"Failed to resume world: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down a world. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> Shutdown(string worldId)
        {
            if (_managementGrain == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                return await _managementGrain.ShutdownWorldAsync(worldId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error shutting down world: {ex.Message}");
                return OperationResult.Error($"Failed to shutdown world: {ex.Message}");
            }
        }
    }
}

