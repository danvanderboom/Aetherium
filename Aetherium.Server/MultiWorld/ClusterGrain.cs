using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain implementation for managing world clusters, economy, and portal networks.
    /// </summary>
    public class ClusterGrain : Grain, IClusterGrain
    {
        private readonly IPersistentState<ClusterState> _state;

        public ClusterGrain(
            [PersistentState("cluster", "worldStore")] IPersistentState<ClusterState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new ClusterState
                {
                    Info = new ClusterInfo
                    {
                        ClusterId = this.GetPrimaryKeyString(),
                        Name = "Unnamed Cluster",
                        WorldIds = new HashSet<string>()
                    },
                    Economy = new ClusterEconomyState
                    {
                        ClusterId = this.GetPrimaryKeyString(),
                        Markets = new Dictionary<string, Market>(),
                        TradeRoutes = new List<TradeRoute>(),
                        TransportSchedules = new List<TransportSchedule>()
                    },
                    Portals = new Dictionary<string, PortalLink>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task InitializeAsync(ClusterInfo clusterInfo)
        {
            clusterInfo.ClusterId = this.GetPrimaryKeyString();
            _state.State.Info = clusterInfo;
            return _state.WriteStateAsync();
        }

        public Task<ClusterInfo?> GetClusterInfoAsync()
        {
            return Task.FromResult<ClusterInfo?>(_state.State?.Info);
        }

        public async Task RegisterWorldAsync(string worldId)
        {
            if (_state.State == null)
                return;

            _state.State.Info.WorldIds.Add(worldId);
            await _state.WriteStateAsync();
        }

        public async Task RegisterMapAsync(string worldId, string mapId)
        {
            if (_state.State == null)
                return;

            var marketKey = $"{worldId}:{mapId}";
            if (!_state.State.Economy.Markets.ContainsKey(marketKey))
            {
                _state.State.Economy.Markets[marketKey] = new Market
                {
                    MarketId = marketKey,
                    WorldId = worldId,
                    MapId = mapId,
                    ResourcePrices = new Dictionary<string, ResourcePricing>(),
                    ResourceAvailability = new Dictionary<string, int>()
                };
            }

            await _state.WriteStateAsync();
        }

        public async Task RegisterPortalAsync(PortalLink portalLink)
        {
            if (_state.State == null)
                return;

            _state.State.Portals[portalLink.PortalId] = portalLink;
            await _state.WriteStateAsync();
        }

        public async Task<(string? worldId, string? mapId)> ResolvePortalTargetAsync(string portalId, string? targetTag = null)
        {
            if (_state.State == null || !_state.State.Portals.TryGetValue(portalId, out var portal))
            {
                return (null, null);
            }

            // If already resolved, return cached target
            if (portal.IsResolved && portal.TargetWorldId != null)
            {
                return (portal.TargetWorldId, portal.TargetMapId);
            }

            // Resolve based on target tag
            if (portal.TargetTag != null || targetTag != null)
            {
                var tag = targetTag ?? portal.TargetTag;
                var target = await ResolveTargetByTagAsync(tag);
                if (target.worldId != null)
                {
                    portal.TargetWorldId = target.worldId;
                    portal.TargetMapId = target.mapId;
                    portal.IsResolved = true;
                    await _state.WriteStateAsync();
                    return target;
                }
            }

            return (null, null);
        }

        private Task<(string? worldId, string? mapId)> ResolveTargetByTagAsync(string tag)
        {
            if (_state.State == null)
                return Task.FromResult<(string?, string?)>((null, null));

            // Simple resolution: find first map in a world matching the tag
            // In a full implementation, this could query world metadata or use more sophisticated matching
            foreach (var worldId in _state.State.Info.WorldIds)
            {
                // Try to find a matching market (which implies a map exists)
                var matchingMarket = _state.State.Economy.Markets.Values
                    .FirstOrDefault(m => m.WorldId == worldId);

                if (matchingMarket != null)
                {
                    return Task.FromResult<(string?, string?)>((matchingMarket.WorldId, matchingMarket.MapId));
                }
            }

            return Task.FromResult<(string?, string?)>((null, null));
        }

        public Task<List<PortalLink>> GetPortalsAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new List<PortalLink>());

            return Task.FromResult(_state.State.Portals.Values.ToList());
        }

        public Task<Market?> GetMarketAsync(string worldId, string mapId)
        {
            if (_state.State == null)
                return Task.FromResult<Market?>(null);

            var marketKey = $"{worldId}:{mapId}";
            _state.State.Economy.Markets.TryGetValue(marketKey, out var market);
            return Task.FromResult<Market?>(market);
        }

        public async Task<TradeRoute> CreateTradeRouteAsync(TradeRoute route)
        {
            if (_state.State == null)
                throw new InvalidOperationException("Cluster not initialized");

            if (string.IsNullOrEmpty(route.RouteId))
            {
                route.RouteId = $"route-{Guid.NewGuid():N}";
            }

            var existing = _state.State.Economy.TradeRoutes.FirstOrDefault(r => r.RouteId == route.RouteId);
            if (existing != null)
            {
                // Update existing route
                var index = _state.State.Economy.TradeRoutes.IndexOf(existing);
                _state.State.Economy.TradeRoutes[index] = route;
            }
            else
            {
                _state.State.Economy.TradeRoutes.Add(route);
            }

            await _state.WriteStateAsync();
            return route;
        }

        public async Task<TransportSchedule> ScheduleTransportAsync(TradeRoute route, Dictionary<string, int> cargo, DateTime departureTime)
        {
            if (_state.State == null)
                throw new InvalidOperationException("Cluster not initialized");

            var schedule = new TransportSchedule
            {
                ScheduleId = $"transport-{Guid.NewGuid():N}",
                RouteId = route.RouteId,
                DepartureTime = departureTime,
                ArrivalTime = departureTime.Add(route.TravelTime),
                Cargo = new Dictionary<string, int>(cargo),
                Status = TransportStatus.Scheduled
            };

            _state.State.Economy.TransportSchedules.Add(schedule);
            await _state.WriteStateAsync();

            return schedule;
        }

        public async Task TickEconomyAsync()
        {
            if (_state.State == null)
                return;

            var now = DateTime.UtcNow;
            var economy = _state.State.Economy;

            // Process transports
            foreach (var transport in economy.TransportSchedules.ToList())
            {
                if (transport.Status == TransportStatus.Scheduled && transport.DepartureTime <= now)
                {
                    transport.Status = TransportStatus.InTransit;
                }
                else if (transport.Status == TransportStatus.InTransit && transport.ArrivalTime <= now)
                {
                    // Arrive at destination
                    transport.Status = TransportStatus.Arrived;
                    
                    // Find route and update destination market
                    var route = economy.TradeRoutes.FirstOrDefault(r => r.RouteId == transport.RouteId);
                    if (route != null)
                    {
                        var destMarket = economy.Markets.Values.FirstOrDefault(m => m.MarketId == route.DestinationMarketId);
                        if (destMarket != null)
                        {
                            foreach (var cargoItem in transport.Cargo)
                            {
                                if (!destMarket.ResourceAvailability.ContainsKey(cargoItem.Key))
                                    destMarket.ResourceAvailability[cargoItem.Key] = 0;
                                destMarket.ResourceAvailability[cargoItem.Key] += cargoItem.Value;

                                // Update pricing based on supply
                                if (!destMarket.ResourcePrices.ContainsKey(cargoItem.Key))
                                {
                                    destMarket.ResourcePrices[cargoItem.Key] = new ResourcePricing
                                    {
                                        ResourceType = cargoItem.Key,
                                        BasePrice = 1.0,
                                        CurrentPrice = 1.0,
                                        Supply = cargoItem.Value,
                                        Demand = 0
                                    };
                                }
                                else
                                {
                                    destMarket.ResourcePrices[cargoItem.Key].Supply += cargoItem.Value;
                                }
                            }
                        }
                    }

                    transport.Status = TransportStatus.Completed;
                }
            }

            // Remove completed transports older than 1 hour
            economy.TransportSchedules.RemoveAll(t => 
                t.Status == TransportStatus.Completed && 
                t.ArrivalTime.AddHours(1) < now);

            // Update prices based on supply/demand
            foreach (var market in economy.Markets.Values)
            {
                foreach (var pricing in market.ResourcePrices.Values)
                {
                    // Simple price adjustment: high supply -> lower price, high demand -> higher price
                    var supplyRatio = pricing.Supply > 0 ? pricing.Demand / pricing.Supply : 1.0;
                    pricing.CurrentPrice = pricing.BasePrice * (1.0 + (supplyRatio - 1.0) * 0.5); // Max 50% price variation
                }
            }

            economy.LastTickAt = now;
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// Persisted state for a cluster grain.
    /// </summary>
    public class ClusterState
    {
        public ClusterInfo Info { get; set; } = new ClusterInfo();
        public ClusterEconomyState Economy { get; set; } = new ClusterEconomyState();
        public Dictionary<string, PortalLink> Portals { get; set; } = new Dictionary<string, PortalLink>();
    }
}

