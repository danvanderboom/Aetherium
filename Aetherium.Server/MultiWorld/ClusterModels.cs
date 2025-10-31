using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Information about a world cluster (group of worlds sharing economy and portals).
    /// </summary>
    [GenerateSerializer]
    public class ClusterInfo
    {
        [Id(0)] public string ClusterId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public HashSet<string> WorldIds { get; set; } = new HashSet<string>();
        [Id(4)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Economy state for a cluster, including markets, trade routes, and transport schedules.
    /// </summary>
    [GenerateSerializer]
    public class ClusterEconomyState
    {
        [Id(0)] public string ClusterId { get; set; } = string.Empty;
        [Id(1)] public Dictionary<string, Market> Markets { get; set; } = new Dictionary<string, Market>(); // Key: "{WorldId}:{MapId}"
        [Id(2)] public List<TradeRoute> TradeRoutes { get; set; } = new List<TradeRoute>();
        [Id(3)] public List<TransportSchedule> TransportSchedules { get; set; } = new List<TransportSchedule>();
        [Id(4)] public DateTime LastTickAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Market for a specific world/map location.
    /// </summary>
    [GenerateSerializer]
    public class Market
    {
        [Id(0)] public string MarketId { get; set; } = string.Empty;
        [Id(1)] public string WorldId { get; set; } = string.Empty;
        [Id(2)] public string MapId { get; set; } = string.Empty;
        [Id(3)] public Dictionary<string, ResourcePricing> ResourcePrices { get; set; } = new Dictionary<string, ResourcePricing>();
        [Id(4)] public Dictionary<string, int> ResourceAvailability { get; set; } = new Dictionary<string, int>(); // Resource type -> quantity
    }

    /// <summary>
    /// Pricing information for a resource type.
    /// </summary>
    [GenerateSerializer]
    public class ResourcePricing
    {
        [Id(0)] public string ResourceType { get; set; } = string.Empty;
        [Id(1)] public double BasePrice { get; set; }
        [Id(2)] public double CurrentPrice { get; set; }
        [Id(3)] public double Supply { get; set; } // Available quantity
        [Id(4)] public double Demand { get; set; } // Requested quantity
    }

    /// <summary>
    /// Trade route between two markets.
    /// </summary>
    [GenerateSerializer]
    public class TradeRoute
    {
        [Id(0)] public string RouteId { get; set; } = string.Empty;
        [Id(1)] public string SourceMarketId { get; set; } = string.Empty;
        [Id(2)] public string DestinationMarketId { get; set; } = string.Empty;
        [Id(3)] public List<string> ResourceTypes { get; set; } = new List<string>();
        [Id(4)] public int Capacity { get; set; } // Maximum cargo per transport
        [Id(5)] public TimeSpan TravelTime { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Scheduled transport for moving resources between markets.
    /// </summary>
    [GenerateSerializer]
    public class TransportSchedule
    {
        [Id(0)] public string ScheduleId { get; set; } = string.Empty;
        [Id(1)] public string RouteId { get; set; } = string.Empty;
        [Id(2)] public DateTime DepartureTime { get; set; }
        [Id(3)] public DateTime ArrivalTime { get; set; }
        [Id(4)] public Dictionary<string, int> Cargo { get; set; } = new Dictionary<string, int>(); // Resource type -> quantity
        [Id(5)] public TransportStatus Status { get; set; } = TransportStatus.Scheduled;
    }

    /// <summary>
    /// Status of a transport.
    /// </summary>
    [GenerateSerializer]
    public enum TransportStatus
    {
        Scheduled,
        InTransit,
        Arrived,
        Completed
    }

    /// <summary>
    /// Portal link metadata for resolving target destinations.
    /// </summary>
    [GenerateSerializer]
    public class PortalLink
    {
        [Id(0)] public string PortalId { get; set; } = string.Empty;
        [Id(1)] public string SourceWorldId { get; set; } = string.Empty;
        [Id(2)] public string SourceMapId { get; set; } = string.Empty;
        [Id(3)] public string? TargetWorldId { get; set; } // Resolved at runtime
        [Id(4)] public string? TargetMapId { get; set; } // Resolved at runtime
        [Id(5)] public string? TargetTag { get; set; } // Link hint (e.g., "hub", "city")
        [Id(6)] public bool IsResolved { get; set; }
    }
}

