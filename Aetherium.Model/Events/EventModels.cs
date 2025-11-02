using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Worlds;

namespace Aetherium.Model.Events
{
    /// <summary>
    /// Unique identifier for an event instance.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct EventInstanceId(string Value);

    /// <summary>
    /// Configuration for creating an event instance.
    /// </summary>
    [GenerateSerializer]
    public class EventInstanceConfig
    {
        [Id(0)] public EventInstanceId EventInstanceId { get; set; }
        [Id(1)] public string EventId { get; set; } = string.Empty;
        [Id(2)] public string EventType { get; set; } = string.Empty;
        [Id(3)] public WorldId WorldId { get; set; }
        [Id(4)] public string? MapId { get; set; }
        [Id(5)] public string? RegionId { get; set; }
        [Id(6)] public int? X { get; set; }
        [Id(7)] public int? Y { get; set; }
        [Id(8)] public int? Z { get; set; }
        [Id(9)] public int AreaOfInterestRadius { get; set; } = 50; // Default AOI radius
        [Id(10)] public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
        [Id(11)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Id(12)] public double ScheduledGameTime { get; set; }
    }

    /// <summary>
    /// Current state of an event instance.
    /// </summary>
    [GenerateSerializer]
    public enum EventInstanceState
    {
        Scheduled,
        Starting,
        Active,
        Completing,
        Completed,
        Cancelled
    }

    /// <summary>
    /// Information about an event instance.
    /// </summary>
    [GenerateSerializer]
    public class EventInstanceInfo
    {
        [Id(0)] public EventInstanceId EventInstanceId { get; set; }
        [Id(1)] public string EventId { get; set; } = string.Empty;
        [Id(2)] public string EventType { get; set; } = string.Empty;
        [Id(3)] public WorldId WorldId { get; set; }
        [Id(4)] public string? MapId { get; set; }
        [Id(5)] public string? RegionId { get; set; }
        [Id(6)] public int? X { get; set; }
        [Id(7)] public int? Y { get; set; }
        [Id(8)] public int? Z { get; set; }
        [Id(9)] public int AreaOfInterestRadius { get; set; }
        [Id(10)] public EventInstanceState State { get; set; }
        [Id(11)] public DateTime CreatedAt { get; set; }
        [Id(12)] public DateTime? StartedAt { get; set; }
        [Id(13)] public DateTime? CompletedAt { get; set; }
        [Id(14)] public double ScheduledGameTime { get; set; }
        [Id(15)] public List<PlayerId> PlayersInArea { get; set; } = new List<PlayerId>();
    }
}

