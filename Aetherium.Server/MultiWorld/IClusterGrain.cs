using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain interface for managing world clusters, economy, and portal networks.
    /// </summary>
    public interface IClusterGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initialize a new cluster.
        /// </summary>
        Task InitializeAsync(ClusterInfo clusterInfo);

        /// <summary>
        /// Get cluster information.
        /// </summary>
        Task<ClusterInfo?> GetClusterInfoAsync();

        /// <summary>
        /// Register a world with this cluster.
        /// </summary>
        Task RegisterWorldAsync(string worldId);

        /// <summary>
        /// Register a map with this cluster and create its market.
        /// </summary>
        Task RegisterMapAsync(string worldId, string mapId);

        /// <summary>
        /// Register a portal for link resolution.
        /// </summary>
        Task RegisterPortalAsync(PortalLink portalLink);

        /// <summary>
        /// Resolve portal target based on link hints.
        /// </summary>
        Task<(string? worldId, string? mapId)> ResolvePortalTargetAsync(string portalId, string? targetTag = null);

        /// <summary>
        /// Get all portals in the cluster.
        /// </summary>
        Task<List<PortalLink>> GetPortalsAsync();

        /// <summary>
        /// Get market for a specific world/map.
        /// </summary>
        Task<Market?> GetMarketAsync(string worldId, string mapId);

        /// <summary>
        /// Create or update a trade route.
        /// </summary>
        Task<TradeRoute> CreateTradeRouteAsync(TradeRoute route);

        /// <summary>
        /// Schedule a transport.
        /// </summary>
        Task<TransportSchedule> ScheduleTransportAsync(TradeRoute route, Dictionary<string, int> cargo, DateTime departureTime);

        /// <summary>
        /// Tick the economy (update prices, process transports).
        /// </summary>
        Task TickEconomyAsync();
    }
}

