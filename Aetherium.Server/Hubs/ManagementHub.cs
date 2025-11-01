using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Orleans;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.MetaProgression;
using Aetherium.Model;

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

        // Cluster Management Methods

        /// <summary>
        /// Lists all clusters.
        /// </summary>
        public async Task<List<ClusterInfo>> ListClusters()
        {
            if (_clusterClient == null || _managementGrain == null)
                return new List<ClusterInfo>();

            try
            {
                var worlds = await _managementGrain.ListWorldsAsync();
                var clusterIds = worlds
                    .Where(w => !string.IsNullOrEmpty(w.ClusterId))
                    .Select(w => w.ClusterId!)
                    .Distinct()
                    .ToList();

                var clusters = new List<ClusterInfo>();
                foreach (var clusterId in clusterIds)
                {
                    var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                    var clusterInfo = await clusterGrain.GetClusterInfoAsync();
                    if (clusterInfo != null)
                        clusters.Add(clusterInfo);
                }

                return clusters;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error listing clusters: {ex.Message}");
                return new List<ClusterInfo>();
            }
        }

        /// <summary>
        /// Gets cluster information by ID.
        /// </summary>
        public async Task<ClusterInfo?> GetCluster(string clusterId)
        {
            if (_clusterClient == null)
                return null;

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                return await clusterGrain.GetClusterInfoAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting cluster: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new cluster. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<ClusterInfo?> CreateCluster(string? clusterId, string name, string? description = null)
        {
            if (_clusterClient == null)
                throw new InvalidOperationException("Orleans is not available");

            try
            {
                var id = clusterId ?? Guid.NewGuid().ToString();
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(id);
                
                var clusterInfo = new ClusterInfo
                {
                    ClusterId = id,
                    Name = name,
                    Description = description ?? string.Empty,
                    WorldIds = new HashSet<string>()
                };

                await clusterGrain.InitializeAsync(clusterInfo);
                return clusterInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error creating cluster: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets all worlds in a cluster.
        /// </summary>
        public async Task<List<WorldInfo>> GetClusterWorlds(string clusterId)
        {
            if (_clusterClient == null || _managementGrain == null)
                return new List<WorldInfo>();

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                var clusterInfo = await clusterGrain.GetClusterInfoAsync();
                
                if (clusterInfo == null)
                    return new List<WorldInfo>();

                var worlds = new List<WorldInfo>();
                foreach (var worldId in clusterInfo.WorldIds)
                {
                    var worldInfo = await _managementGrain.GetWorldInfoAsync(worldId);
                    if (worldInfo != null)
                        worlds.Add(worldInfo);
                }

                return worlds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting cluster worlds: {ex.Message}");
                return new List<WorldInfo>();
            }
        }

        /// <summary>
        /// Gets economy state for a cluster.
        /// </summary>
        public async Task<ClusterEconomyState?> GetClusterEconomy(string clusterId)
        {
            if (_clusterClient == null)
                return null;

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                return await clusterGrain.GetEconomyStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting cluster economy: {ex.Message}");
                return null;
            }
        }

        // Portal Management Methods

        /// <summary>
        /// Gets all portals in a cluster.
        /// </summary>
        public async Task<List<PortalLink>> GetPortals(string clusterId)
        {
            if (_clusterClient == null)
                return new List<PortalLink>();

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                return await clusterGrain.GetPortalsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting portals: {ex.Message}");
                return new List<PortalLink>();
            }
        }

        /// <summary>
        /// Registers a portal in a cluster. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<PortalLink?> RegisterPortal(string clusterId, string? portalId, string sourceWorldId, string sourceMapId, string? targetWorldId = null, string? targetMapId = null, string? targetTag = null)
        {
            if (_clusterClient == null)
                throw new InvalidOperationException("Orleans is not available");

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                
                var portalLink = new PortalLink
                {
                    PortalId = portalId ?? Guid.NewGuid().ToString(),
                    SourceWorldId = sourceWorldId,
                    SourceMapId = sourceMapId,
                    TargetWorldId = targetWorldId,
                    TargetMapId = targetMapId,
                    TargetTag = targetTag,
                    IsResolved = targetWorldId != null && targetMapId != null
                };

                await clusterGrain.RegisterPortalAsync(portalLink);
                return portalLink;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error registering portal: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Resolves a portal target.
        /// </summary>
        public async Task<(string? worldId, string? mapId)> ResolvePortal(string clusterId, string portalId, string? targetTag = null)
        {
            if (_clusterClient == null)
                return (null, null);

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                return await clusterGrain.ResolvePortalTargetAsync(portalId, targetTag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error resolving portal: {ex.Message}");
                return (null, null);
            }
        }

        // Transport Management Methods

        /// <summary>
        /// Gets all transport schedules in a cluster.
        /// </summary>
        public async Task<List<TransportSchedule>> GetTransports(string clusterId)
        {
            if (_clusterClient == null)
                return new List<TransportSchedule>();

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                return await clusterGrain.GetTransportSchedulesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting transports: {ex.Message}");
                return new List<TransportSchedule>();
            }
        }

        /// <summary>
        /// Creates a trade route. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<TradeRoute?> CreateTradeRoute(string clusterId, string? routeId, string sourceMarketId, string destinationMarketId, List<string>? resourceTypes = null, int capacity = 100, TimeSpan? travelTime = null)
        {
            if (_clusterClient == null)
                throw new InvalidOperationException("Orleans is not available");

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                
                var route = new TradeRoute
                {
                    RouteId = routeId ?? Guid.NewGuid().ToString(),
                    SourceMarketId = sourceMarketId,
                    DestinationMarketId = destinationMarketId,
                    ResourceTypes = resourceTypes ?? new List<string>(),
                    Capacity = capacity,
                    TravelTime = travelTime ?? TimeSpan.FromHours(1)
                };

                return await clusterGrain.CreateTradeRouteAsync(route);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error creating trade route: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Schedules a transport. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<TransportSchedule?> ScheduleTransport(string clusterId, string routeId, string sourceMarketId, string destinationMarketId, Dictionary<string, int>? cargo = null, DateTime? departureTime = null)
        {
            if (_clusterClient == null)
                throw new InvalidOperationException("Orleans is not available");

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                
                // Create route from parameters (in a full implementation, we'd fetch the route)
                var route = new TradeRoute
                {
                    RouteId = routeId,
                    SourceMarketId = sourceMarketId,
                    DestinationMarketId = destinationMarketId,
                    ResourceTypes = cargo?.Keys.ToList() ?? new List<string>(),
                    Capacity = cargo?.Values.Sum() ?? 0,
                    TravelTime = TimeSpan.FromHours(1) // Default
                };

                var schedule = await clusterGrain.ScheduleTransportAsync(
                    route, 
                    cargo ?? new Dictionary<string, int>(), 
                    departureTime ?? DateTime.UtcNow);

                return schedule;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error scheduling transport: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ticks the economy (updates prices, processes transports). Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> TickEconomy(string clusterId)
        {
            if (_clusterClient == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                var clusterGrain = _clusterClient.GetGrain<IClusterGrain>(clusterId);
                await clusterGrain.TickEconomyAsync();
                return OperationResult.Ok("Economy ticked successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error ticking economy: {ex.Message}");
                return OperationResult.Error($"Failed to tick economy: {ex.Message}");
            }
        }

        // Meta-Progression Methods

        /// <summary>
        /// Gets meta-progression state for a player.
        /// </summary>
        public async Task<MetaProgressionState?> GetMetaProgressionState(string playerId)
        {
            if (_clusterClient == null)
                return null;

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                return await metaProgGrain.GetStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting meta-progression state: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all discoveries for a player.
        /// </summary>
        public async Task<Dictionary<string, object>?> GetDiscoveries(string playerId)
        {
            if (_clusterClient == null)
                return null;

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                var state = await metaProgGrain.GetStateAsync();

                if (state == null)
                    return null;

                return new Dictionary<string, object>
                {
                    ["visitedWorldIds"] = state.VisitedWorldIds.ToList(),
                    ["visitedMapIds"] = state.VisitedMapIds.ToList(),
                    ["discoveredWorldTemplates"] = state.DiscoveredWorldTemplates.ToList(),
                    ["discoveredTags"] = state.DiscoveredTags.ToList(),
                    ["completedQuestIds"] = state.CompletedQuestIds.ToList(),
                    ["completedCrossWorldQuestIds"] = state.CompletedCrossWorldQuestIds.ToList(),
                    ["tagVisitCounts"] = state.TagVisitCounts
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting discoveries: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all unlocked generators for a player.
        /// </summary>
        public async Task<List<string>> GetUnlocks(string playerId)
        {
            if (_clusterClient == null)
                return new List<string>();

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                return await metaProgGrain.GetAllowedGeneratorsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error getting unlocks: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Records a discovery for a player. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> RecordDiscovery(string playerId, string worldId, string mapId, string? worldTemplate = null, List<string>? tags = null)
        {
            if (_clusterClient == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.RecordDiscoveryAsync(worldId, mapId, worldTemplate, tags);
                return OperationResult.Ok("Discovery recorded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error recording discovery: {ex.Message}");
                return OperationResult.Error($"Failed to record discovery: {ex.Message}");
            }
        }

        /// <summary>
        /// Records a quest completion for a player. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> RecordQuestCompletion(string playerId, string questId, bool isCrossWorld = false)
        {
            if (_clusterClient == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.RecordQuestCompletionAsync(questId, isCrossWorld);
                return OperationResult.Ok("Quest completion recorded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error recording quest completion: {ex.Message}");
                return OperationResult.Error($"Failed to record quest completion: {ex.Message}");
            }
        }

        /// <summary>
        /// Evaluates unlock criteria and unlocks new generators if conditions are met. Requires Admin role.
        /// </summary>
        [Authorize(Policy = "Admin")]
        public async Task<OperationResult> EvaluateUnlocks(string playerId)
        {
            if (_clusterClient == null)
                return OperationResult.Error("Orleans is not available");

            try
            {
                var metaProgGrain = _clusterClient.GetGrain<IMetaProgressionGrain>(playerId);
                await metaProgGrain.EvaluateUnlocksAsync();
                return OperationResult.Ok("Unlocks evaluated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManagementHub] Error evaluating unlocks: {ex.Message}");
                return OperationResult.Error($"Failed to evaluate unlocks: {ex.Message}");
            }
        }
    }
}

