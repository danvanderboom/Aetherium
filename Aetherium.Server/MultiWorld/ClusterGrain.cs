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
        private IDisposable? _economyTimer;

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

            // Start periodic economy ticking if cluster has active worlds
            // Tick every 5 minutes (economy time) - adjust as needed
            if (_state.State.Info.WorldIds.Count > 0)
            {
                _economyTimer = RegisterTimer(
                    async _ => await TickEconomyAsync(),
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _economyTimer?.Dispose();
            return base.OnDeactivateAsync(reason, cancellationToken);
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

            // Start economy timer if this is the first world
            if (_state.State.Info.WorldIds.Count == 1 && _economyTimer == null)
            {
                _economyTimer = RegisterTimer(
                    async _ => await TickEconomyAsync(),
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
            }
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

            // Fallback: if any markets exist, return the first available world/map to keep portals functional
            var anyMarket = _state.State.Economy.Markets.Values.FirstOrDefault();
            if (anyMarket != null)
            {
                return (anyMarket.WorldId, anyMarket.MapId);
            }

            return (null, null);
        }

        private async Task<(string? worldId, string? mapId)> ResolveTargetByTagAsync(string tag)
        {
            if (_state.State == null)
                return (null, null);

            if (string.IsNullOrEmpty(tag))
                return (null, null);

            var tagLower = tag.ToLowerInvariant();

            // Query worlds and maps to find matches by tag
            foreach (var worldId in _state.State.Info.WorldIds)
            {
                var worldGrain = GrainFactory.GetGrain<IWorldGrain>(worldId);
                var worldInfo = await worldGrain.GetInfoAsync();
                
                if (worldInfo == null)
                    continue;

                // Check world metadata for tags
                if (worldInfo.Metadata != null)
                {
                    if (worldInfo.Metadata.TryGetValue("tags", out var tagsObj) && tagsObj is List<string> tags)
                    {
                        if (tags.Any(t => t.ToLowerInvariant() == tagLower))
                        {
                            // Found matching world, return first map
                            if (worldInfo.MapIds != null && worldInfo.MapIds.Count > 0)
                            {
                                return (worldId, worldInfo.MapIds[0]);
                            }
                        }
                    }

                    // Check if tag matches generator type hint in metadata
                    if (worldInfo.Metadata.TryGetValue("generatorType", out var genTypeObj))
                    {
                        var genType = genTypeObj?.ToString() ?? "";
                        if (genType.ToLowerInvariant().Contains(tagLower))
                        {
                            if (worldInfo.MapIds != null && worldInfo.MapIds.Count > 0)
                            {
                                return (worldId, worldInfo.MapIds[0]);
                            }
                        }
                    }
                }

                // Query maps in this world for tag matches
                if (worldInfo.MapIds != null)
                {
                    foreach (var mapId in worldInfo.MapIds)
                    {
                        var mapGrain = GrainFactory.GetGrain<IGameMapGrain>(mapId);
                        var mapMetadata = await mapGrain.GetMetadataAsync();
                        
                        if (mapMetadata != null)
                        {
                            // Check if generator type matches tag (e.g., "hub" in generator type)
                            if (mapMetadata.GeneratorType != null && 
                                mapMetadata.GeneratorType.ToLowerInvariant().Contains(tagLower))
                            {
                                return (worldId, mapId);
                            }

                            // Check map name for tag matches
                            if (mapMetadata.MapName != null && 
                                mapMetadata.MapName.ToLowerInvariant().Contains(tagLower))
                            {
                                return (worldId, mapId);
                            }
                        }
                    }
                }
            }

            // Fallback: return first available world/map if tag is generic
            if (tagLower == "any" || tagLower == "random")
            {
                var firstWorldId = _state.State.Info.WorldIds.FirstOrDefault();
                if (firstWorldId != null)
                {
                    var worldGrain = GrainFactory.GetGrain<IWorldGrain>(firstWorldId);
                    var worldInfo = await worldGrain.GetInfoAsync();
                    if (worldInfo?.MapIds != null && worldInfo.MapIds.Count > 0)
                    {
                        return (firstWorldId, worldInfo.MapIds[0]);
                    }
                }
            }

            return (null, null);
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

            // Process existing transports
            foreach (var transport in economy.TransportSchedules.ToList())
            {
                if (transport.Status == TransportStatus.Scheduled && transport.DepartureTime <= now)
                {
                    // Depart - remove resources from source market
                    transport.Status = TransportStatus.InTransit;
                    
                    var route = economy.TradeRoutes.FirstOrDefault(r => r.RouteId == transport.RouteId);
                    if (route != null)
                    {
                        var sourceMarket = economy.Markets.Values.FirstOrDefault(m => m.MarketId == route.SourceMarketId);
                        if (sourceMarket != null)
                        {
                            // Remove cargo from source market
                            foreach (var cargoItem in transport.Cargo)
                            {
                                if (sourceMarket.ResourceAvailability.ContainsKey(cargoItem.Key))
                                {
                                    sourceMarket.ResourceAvailability[cargoItem.Key] = 
                                        Math.Max(0, sourceMarket.ResourceAvailability[cargoItem.Key] - cargoItem.Value);
                                    
                                    // Update supply in pricing
                                    if (sourceMarket.ResourcePrices.ContainsKey(cargoItem.Key))
                                    {
                                        sourceMarket.ResourcePrices[cargoItem.Key].Supply = 
                                            Math.Max(0, sourceMarket.ResourcePrices[cargoItem.Key].Supply - cargoItem.Value);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (transport.Status == TransportStatus.InTransit && transport.ArrivalTime <= now)
                {
                    // Arrive at destination - add resources to destination market
                    transport.Status = TransportStatus.Arrived;
                    
                    var route = economy.TradeRoutes.FirstOrDefault(r => r.RouteId == transport.RouteId);
                    if (route != null)
                    {
                        var destMarket = economy.Markets.Values.FirstOrDefault(m => m.MarketId == route.DestinationMarketId);
                        if (destMarket != null)
                        {
                            foreach (var cargoItem in transport.Cargo)
                            {
                                // Add to availability
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

                    // Mark as completed after a short delay (resources have been delivered)
                    transport.Status = TransportStatus.Completed;
                }
            }

            // Remove completed transports older than 1 hour
            economy.TransportSchedules.RemoveAll(t => 
                t.Status == TransportStatus.Completed && 
                t.ArrivalTime.AddHours(1) < now);

            // Create new transports for trade routes that don't have pending transports
            foreach (var route in economy.TradeRoutes)
            {
                // Check if there are any pending transports for this route
                var hasPendingTransport = economy.TransportSchedules.Any(t => 
                    t.RouteId == route.RouteId && 
                    (t.Status == TransportStatus.Scheduled || t.Status == TransportStatus.InTransit));

                if (!hasPendingTransport)
                {
                    // Try to create a new transport if source market has available resources
                    var sourceMarket = economy.Markets.Values.FirstOrDefault(m => m.MarketId == route.SourceMarketId);
                    if (sourceMarket != null)
                    {
                        var cargo = new Dictionary<string, int>();
                        
                        // Load available resources up to capacity
                        var remainingCapacity = route.Capacity;
                        foreach (var resourceType in route.ResourceTypes)
                        {
                            if (remainingCapacity <= 0)
                                break;
                                
                            if (sourceMarket.ResourceAvailability.TryGetValue(resourceType, out var available) && available > 0)
                            {
                                var quantity = Math.Min(available, remainingCapacity);
                                cargo[resourceType] = quantity;
                                remainingCapacity -= quantity;
                            }
                        }

                        // Only schedule if there's cargo to transport
                        if (cargo.Count > 0)
                        {
                            var nextDeparture = now.AddMinutes(5); // Schedule departure in 5 minutes
                            var schedule = new TransportSchedule
                            {
                                ScheduleId = $"transport-{Guid.NewGuid():N}",
                                RouteId = route.RouteId,
                                DepartureTime = nextDeparture,
                                ArrivalTime = nextDeparture.Add(route.TravelTime),
                                Cargo = cargo,
                                Status = TransportStatus.Scheduled
                            };

                            economy.TransportSchedules.Add(schedule);
                        }
                    }
                }
            }

            // Update prices based on supply/demand
            foreach (var market in economy.Markets.Values)
            {
                foreach (var pricing in market.ResourcePrices.Values)
                {
                    // Calculate price based on supply/demand ratio
                    // High supply -> lower price, high demand -> higher price
                    var supplyDemandRatio = pricing.Supply > 0 ? pricing.Demand / pricing.Supply : 1.0;
                    
                    // Price adjustment: 50% variation based on ratio
                    // If supply >> demand (ratio < 1), price decreases
                    // If demand >> supply (ratio > 1), price increases
                    var priceMultiplier = 1.0 + (supplyDemandRatio - 1.0) * 0.5;
                    
                    // Clamp price variation to reasonable bounds (0.5x to 2x base price)
                    priceMultiplier = Math.Max(0.5, Math.Min(2.0, priceMultiplier));
                    
                    pricing.CurrentPrice = pricing.BasePrice * priceMultiplier;
                }
            }

            economy.LastTickAt = now;
            await _state.WriteStateAsync();
        }

        public Task<ClusterEconomyState?> GetEconomyStateAsync()
        {
            if (_state.State == null)
                return Task.FromResult<ClusterEconomyState?>(null);

            return Task.FromResult<ClusterEconomyState?>(_state.State.Economy);
        }

        public Task<List<TransportSchedule>> GetTransportSchedulesAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new List<TransportSchedule>());

            return Task.FromResult(_state.State.Economy.TransportSchedules.ToList());
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

