using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Management;
using Aetherium.Model;
using Orleans;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for managing world clusters, portals, and economy.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClusterController : ControllerBase
    {
        private readonly IClusterClient _orleansClient;

        public ClusterController(IClusterClient orleansClient)
        {
            _orleansClient = orleansClient;
        }

        /// <summary>
        /// Gets all clusters.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ClusterInfoDto>>> GetAllClusters()
        {
            try
            {
                // Get all worlds and extract unique cluster IDs
                var managementGrain = _orleansClient.GetGrain<IGameManagementGrain>("GLOBAL");
                var worlds = await managementGrain.ListWorldsAsync();
                
                var clusterIds = worlds
                    .Where(w => !string.IsNullOrEmpty(w.ClusterId))
                    .Select(w => w.ClusterId!)
                    .Distinct()
                    .ToList();

                var clusters = new List<ClusterInfoDto>();
                
                // Get cluster info for each unique cluster ID
                foreach (var clusterId in clusterIds)
                {
                    var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                    var clusterInfo = await clusterGrain.GetClusterInfoAsync();
                    
                    if (clusterInfo != null)
                    {
                        clusters.Add(new ClusterInfoDto
                        {
                            ClusterId = clusterInfo.ClusterId,
                            Name = clusterInfo.Name,
                            Description = clusterInfo.Description,
                            WorldIds = clusterInfo.WorldIds.ToList(),
                            CreatedAt = clusterInfo.CreatedAt
                        });
                    }
                }

                return Ok(clusters);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get clusters: {ex.Message}" });
            }
        }

        /// <summary>
        /// Creates a new cluster.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ClusterInfoDto>> CreateCluster([FromBody] CreateClusterRequest request)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(request.ClusterId ?? Guid.NewGuid().ToString());
                
                var clusterInfo = new ClusterInfo
                {
                    ClusterId = request.ClusterId ?? Guid.NewGuid().ToString(),
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    WorldIds = new HashSet<string>()
                };

                await clusterGrain.InitializeAsync(clusterInfo);

                var dto = new ClusterInfoDto
                {
                    ClusterId = clusterInfo.ClusterId,
                    Name = clusterInfo.Name,
                    Description = clusterInfo.Description,
                    WorldIds = clusterInfo.WorldIds.ToList(),
                    CreatedAt = DateTime.UtcNow
                };

                return CreatedAtAction(nameof(GetCluster), new { clusterId = clusterInfo.ClusterId }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to create cluster: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets a cluster by ID.
        /// </summary>
        [HttpGet("{clusterId}")]
        public async Task<ActionResult<ClusterInfoDto>> GetCluster(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var clusterInfo = await clusterGrain.GetClusterInfoAsync();

                if (clusterInfo == null)
                    return NotFound($"Cluster not found: {clusterId}");

                var dto = new ClusterInfoDto
                {
                    ClusterId = clusterInfo.ClusterId,
                    Name = clusterInfo.Name,
                    Description = clusterInfo.Description,
                    WorldIds = clusterInfo.WorldIds.ToList(),
                    CreatedAt = clusterInfo.CreatedAt
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get cluster: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all worlds in a cluster.
        /// </summary>
        [HttpGet("{clusterId}/worlds")]
        public async Task<ActionResult<List<WorldInfoDto>>> GetWorlds(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var clusterInfo = await clusterGrain.GetClusterInfoAsync();

                if (clusterInfo == null)
                    return NotFound($"Cluster not found: {clusterId}");

                var managementGrain = _orleansClient.GetGrain<IGameManagementGrain>("GLOBAL");
                var worlds = new List<WorldInfoDto>();

                // Get info for each world in the cluster
                foreach (var worldId in clusterInfo.WorldIds)
                {
                    var worldInfo = await managementGrain.GetWorldInfoAsync(worldId);
                    if (worldInfo != null)
                    {
                        worlds.Add(new WorldInfoDto
                        {
                            WorldId = worldInfo.WorldId,
                            Name = worldInfo.Name,
                            Description = worldInfo.Description,
                            State = worldInfo.State.ToString(),
                            PlayerCount = worldInfo.PlayerCount,
                            MaxPlayers = worldInfo.MaxPlayers,
                            CreatedAt = worldInfo.CreatedAt,
                            LastActivityAt = worldInfo.LastActivityAt,
                            NarrativeId = worldInfo.NarrativeId,
                            MapIds = worldInfo.MapIds ?? new List<string>(),
                            ClusterId = worldInfo.ClusterId
                        });
                    }
                }

                return Ok(worlds);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get worlds: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all portals in a cluster.
        /// </summary>
        [HttpGet("{clusterId}/portals")]
        public async Task<ActionResult<List<PortalLinkDto>>> GetPortals(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var portals = await clusterGrain.GetPortalsAsync();

                var dtos = portals.Select(p => new PortalLinkDto
                {
                    PortalId = p.PortalId,
                    SourceWorldId = p.SourceWorldId,
                    SourceMapId = p.SourceMapId,
                    TargetWorldId = p.TargetWorldId,
                    TargetMapId = p.TargetMapId,
                    TargetTag = p.TargetTag,
                    IsResolved = p.IsResolved
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get portals: {ex.Message}" });
            }
        }

        /// <summary>
        /// Registers a portal in a cluster.
        /// </summary>
        [HttpPost("{clusterId}/portals")]
        public async Task<ActionResult<PortalLinkDto>> RegisterPortal(string clusterId, [FromBody] RegisterPortalRequest request)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                
                var portalLink = new PortalLink
                {
                    PortalId = request.PortalId ?? Guid.NewGuid().ToString(),
                    SourceWorldId = request.SourceWorldId,
                    SourceMapId = request.SourceMapId,
                    TargetWorldId = request.TargetWorldId,
                    TargetMapId = request.TargetMapId,
                    TargetTag = request.TargetTag,
                    IsResolved = request.TargetWorldId != null && request.TargetMapId != null
                };

                await clusterGrain.RegisterPortalAsync(portalLink);

                var dto = new PortalLinkDto
                {
                    PortalId = portalLink.PortalId,
                    SourceWorldId = portalLink.SourceWorldId,
                    SourceMapId = portalLink.SourceMapId,
                    TargetWorldId = portalLink.TargetWorldId,
                    TargetMapId = portalLink.TargetMapId,
                    TargetTag = portalLink.TargetTag,
                    IsResolved = portalLink.IsResolved
                };

                return CreatedAtAction(nameof(GetPortals), new { clusterId }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to register portal: {ex.Message}" });
            }
        }

        /// <summary>
        /// Resolves a portal target.
        /// </summary>
        [HttpPost("{clusterId}/portals/{portalId}/resolve")]
        public async Task<ActionResult<PortalTargetDto>> ResolvePortalTarget(string clusterId, string portalId, [FromBody] ResolvePortalRequest? request = null)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var (worldId, mapId) = await clusterGrain.ResolvePortalTargetAsync(portalId, request?.TargetTag);

                if (string.IsNullOrEmpty(worldId) || string.IsNullOrEmpty(mapId))
                    return NotFound($"Could not resolve portal target: {portalId}");

                var dto = new PortalTargetDto
                {
                    WorldId = worldId,
                    MapId = mapId
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to resolve portal target: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets market information for a world/map.
        /// </summary>
        [HttpGet("{clusterId}/markets/{worldId}/{mapId}")]
        public async Task<ActionResult<MarketDto>> GetMarket(string clusterId, string worldId, string mapId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var market = await clusterGrain.GetMarketAsync(worldId, mapId);

                if (market == null)
                    return NotFound($"Market not found for {worldId}:{mapId}");

                var dto = new MarketDto
                {
                    MarketId = market.MarketId,
                    WorldId = market.WorldId,
                    MapId = market.MapId,
                    ResourcePrices = market.ResourcePrices.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new ResourcePricingDto
                        {
                            ResourceType = kvp.Value.ResourceType,
                            BasePrice = kvp.Value.BasePrice,
                            CurrentPrice = kvp.Value.CurrentPrice,
                            Supply = kvp.Value.Supply,
                            Demand = kvp.Value.Demand
                        }),
                    ResourceAvailability = market.ResourceAvailability
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get market: {ex.Message}" });
            }
        }

        /// <summary>
        /// Creates or updates a trade route.
        /// </summary>
        [HttpPost("{clusterId}/routes")]
        public async Task<ActionResult<TradeRouteDto>> CreateTradeRoute(string clusterId, [FromBody] CreateTradeRouteRequest request)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                
                var route = new TradeRoute
                {
                    RouteId = request.RouteId ?? Guid.NewGuid().ToString(),
                    SourceMarketId = request.SourceMarketId,
                    DestinationMarketId = request.DestinationMarketId,
                    ResourceTypes = request.ResourceTypes ?? new List<string>(),
                    Capacity = request.Capacity,
                    TravelTime = request.TravelTime
                };

                var createdRoute = await clusterGrain.CreateTradeRouteAsync(route);

                var dto = new TradeRouteDto
                {
                    RouteId = createdRoute.RouteId,
                    SourceMarketId = createdRoute.SourceMarketId,
                    DestinationMarketId = createdRoute.DestinationMarketId,
                    ResourceTypes = createdRoute.ResourceTypes,
                    Capacity = createdRoute.Capacity,
                    TravelTime = createdRoute.TravelTime
                };

                return CreatedAtAction(nameof(GetTradeRoute), new { clusterId, routeId = createdRoute.RouteId }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to create trade route: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets a trade route by ID.
        /// </summary>
        [HttpGet("{clusterId}/routes/{routeId}")]
        public async Task<ActionResult<TradeRouteDto>> GetTradeRoute(string clusterId, string routeId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var clusterInfo = await clusterGrain.GetClusterInfoAsync();

                if (clusterInfo == null)
                    return NotFound($"Cluster not found: {clusterId}");

                // Get economy state to find route
                // Note: In a full implementation, we'd have a GetTradeRouteAsync method
                // For now, we'd need to get the cluster state directly
                return StatusCode(501, new { error = "GetTradeRoute not yet implemented - need GetTradeRouteAsync method" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get trade route: {ex.Message}" });
            }
        }

        /// <summary>
        /// Schedules a transport.
        /// </summary>
        [HttpPost("{clusterId}/transports")]
        public async Task<ActionResult<TransportScheduleDto>> ScheduleTransport(string clusterId, [FromBody] ScheduleTransportRequest request)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                
                // Get route first
                // Note: In a full implementation, we'd need GetTradeRouteAsync
                // For now, create a route from the request
                var route = new TradeRoute
                {
                    RouteId = request.RouteId,
                    SourceMarketId = request.SourceMarketId,
                    DestinationMarketId = request.DestinationMarketId,
                    ResourceTypes = request.Cargo?.Keys.ToList() ?? new List<string>(),
                    Capacity = request.Cargo?.Values.Sum() ?? 0,
                    TravelTime = request.TravelTime
                };

                var schedule = await clusterGrain.ScheduleTransportAsync(route, request.Cargo ?? new Dictionary<string, int>(), request.DepartureTime);

                var dto = new TransportScheduleDto
                {
                    ScheduleId = schedule.ScheduleId,
                    RouteId = schedule.RouteId,
                    DepartureTime = schedule.DepartureTime,
                    ArrivalTime = schedule.ArrivalTime,
                    Cargo = schedule.Cargo,
                    Status = schedule.Status.ToString()
                };

                return CreatedAtAction(nameof(GetTransportSchedule), new { clusterId, scheduleId = schedule.ScheduleId }, dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to schedule transport: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets all transport schedules in a cluster.
        /// </summary>
        [HttpGet("{clusterId}/transports")]
        public async Task<ActionResult<List<TransportScheduleDto>>> GetTransports(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var schedules = await clusterGrain.GetTransportSchedulesAsync();

                var dtos = schedules.Select(s => new TransportScheduleDto
                {
                    ScheduleId = s.ScheduleId,
                    RouteId = s.RouteId,
                    DepartureTime = s.DepartureTime,
                    ArrivalTime = s.ArrivalTime,
                    Cargo = s.Cargo,
                    Status = s.Status.ToString()
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get transports: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets a transport schedule by ID.
        /// </summary>
        [HttpGet("{clusterId}/transports/{scheduleId}")]
        public async Task<ActionResult<TransportScheduleDto>> GetTransportSchedule(string clusterId, string scheduleId)
        {
            try
            {
                // Note: In a full implementation, we'd have a GetTransportScheduleAsync method
                return StatusCode(501, new { error = "GetTransportSchedule not yet implemented - need GetTransportScheduleAsync method" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get transport schedule: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets economy state for a cluster.
        /// </summary>
        [HttpGet("{clusterId}/economy")]
        public async Task<ActionResult<ClusterEconomyDto>> GetEconomy(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                var economy = await clusterGrain.GetEconomyStateAsync();

                if (economy == null)
                    return NotFound($"Economy state not found for cluster: {clusterId}");

                var dto = new ClusterEconomyDto
                {
                    ClusterId = economy.ClusterId,
                    Markets = economy.Markets.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new MarketDto
                        {
                            MarketId = kvp.Value.MarketId,
                            WorldId = kvp.Value.WorldId,
                            MapId = kvp.Value.MapId,
                            ResourcePrices = kvp.Value.ResourcePrices.ToDictionary(
                                pkvp => pkvp.Key,
                                pkvp => new ResourcePricingDto
                                {
                                    ResourceType = pkvp.Value.ResourceType,
                                    BasePrice = pkvp.Value.BasePrice,
                                    CurrentPrice = pkvp.Value.CurrentPrice,
                                    Supply = pkvp.Value.Supply,
                                    Demand = pkvp.Value.Demand
                                }),
                            ResourceAvailability = kvp.Value.ResourceAvailability
                        }),
                    TradeRoutes = economy.TradeRoutes.Select(r => new TradeRouteDto
                    {
                        RouteId = r.RouteId,
                        SourceMarketId = r.SourceMarketId,
                        DestinationMarketId = r.DestinationMarketId,
                        ResourceTypes = r.ResourceTypes,
                        Capacity = r.Capacity,
                        TravelTime = r.TravelTime
                    }).ToList(),
                    TransportSchedules = economy.TransportSchedules.Select(s => new TransportScheduleDto
                    {
                        ScheduleId = s.ScheduleId,
                        RouteId = s.RouteId,
                        DepartureTime = s.DepartureTime,
                        ArrivalTime = s.ArrivalTime,
                        Cargo = s.Cargo,
                        Status = s.Status.ToString()
                    }).ToList(),
                    LastTickAt = economy.LastTickAt
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get economy: {ex.Message}" });
            }
        }

        /// <summary>
        /// Ticks the economy (updates prices, processes transports).
        /// </summary>
        [HttpPost("{clusterId}/economy/tick")]
        public async Task<ActionResult> TickEconomy(string clusterId)
        {
            try
            {
                var clusterGrain = _orleansClient.GetGrain<IClusterGrain>(clusterId);
                await clusterGrain.TickEconomyAsync();
                return Ok(new { message = "Economy ticked successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to tick economy: {ex.Message}" });
            }
        }
    }

    // DTOs
    public class CreateClusterRequest
    {
        public string? ClusterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ClusterInfoDto
    {
        public string ClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> WorldIds { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
    }

    public class PortalLinkDto
    {
        public string PortalId { get; set; } = string.Empty;
        public string SourceWorldId { get; set; } = string.Empty;
        public string SourceMapId { get; set; } = string.Empty;
        public string? TargetWorldId { get; set; }
        public string? TargetMapId { get; set; }
        public string? TargetTag { get; set; }
        public bool IsResolved { get; set; }
    }

    public class ResolvePortalRequest
    {
        public string? TargetTag { get; set; }
    }

    public class PortalTargetDto
    {
        public string WorldId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
    }

    public class MarketDto
    {
        public string MarketId { get; set; } = string.Empty;
        public string WorldId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
        public Dictionary<string, ResourcePricingDto> ResourcePrices { get; set; } = new Dictionary<string, ResourcePricingDto>();
        public Dictionary<string, int> ResourceAvailability { get; set; } = new Dictionary<string, int>();
    }

    public class ResourcePricingDto
    {
        public string ResourceType { get; set; } = string.Empty;
        public double BasePrice { get; set; }
        public double CurrentPrice { get; set; }
        public double Supply { get; set; }
        public double Demand { get; set; }
    }

    public class CreateTradeRouteRequest
    {
        public string? RouteId { get; set; }
        public string SourceMarketId { get; set; } = string.Empty;
        public string DestinationMarketId { get; set; } = string.Empty;
        public List<string>? ResourceTypes { get; set; }
        public int Capacity { get; set; }
        public TimeSpan TravelTime { get; set; }
    }

    public class TradeRouteDto
    {
        public string RouteId { get; set; } = string.Empty;
        public string SourceMarketId { get; set; } = string.Empty;
        public string DestinationMarketId { get; set; } = string.Empty;
        public List<string> ResourceTypes { get; set; } = new List<string>();
        public int Capacity { get; set; }
        public TimeSpan TravelTime { get; set; }
    }

    public class ScheduleTransportRequest
    {
        public string RouteId { get; set; } = string.Empty;
        public string SourceMarketId { get; set; } = string.Empty;
        public string DestinationMarketId { get; set; } = string.Empty;
        public Dictionary<string, int>? Cargo { get; set; }
        public DateTime DepartureTime { get; set; }
        public TimeSpan TravelTime { get; set; }
    }

    public class TransportScheduleDto
    {
        public string ScheduleId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public Dictionary<string, int> Cargo { get; set; } = new Dictionary<string, int>();
        public string Status { get; set; } = string.Empty;
    }

    public class RegisterPortalRequest
    {
        public string? PortalId { get; set; }
        public string SourceWorldId { get; set; } = string.Empty;
        public string SourceMapId { get; set; } = string.Empty;
        public string? TargetWorldId { get; set; }
        public string? TargetMapId { get; set; }
        public string? TargetTag { get; set; }
    }

    public class ClusterEconomyDto
    {
        public string ClusterId { get; set; } = string.Empty;
        public Dictionary<string, MarketDto> Markets { get; set; } = new Dictionary<string, MarketDto>();
        public List<TradeRouteDto> TradeRoutes { get; set; } = new List<TradeRouteDto>();
        public List<TransportScheduleDto> TransportSchedules { get; set; } = new List<TransportScheduleDto>();
        public DateTime LastTickAt { get; set; }
    }
}

