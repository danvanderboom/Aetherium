using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;

namespace Aetherium.Model.Instances
{
    /// <summary>
    /// Configuration for creating a dungeon instance.
    /// </summary>
    [GenerateSerializer]
    public class InstanceConfig
    {
        [Id(0)] public InstanceId InstanceId { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public WorldId WorldId { get; set; }
        [Id(3)] public string DungeonName { get; set; } = string.Empty;
        [Id(4)] public string GeneratorType { get; set; } = "dungeon";
        [Id(5)] public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        [Id(6)] public PartyId? PartyId { get; set; }
        [Id(7)] public List<PlayerId> PlayerIds { get; set; } = new List<PlayerId>();
        [Id(8)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Id(9)] public LockoutKey? LockoutKey { get; set; }
    }

    /// <summary>
    /// Current state of a dungeon instance.
    /// </summary>
    [GenerateSerializer]
    public enum InstanceState
    {
        Creating,
        Active,
        Completed,
        Abandoned,
        ShuttingDown,
        Stopped
    }

    /// <summary>
    /// Information about a dungeon instance.
    /// </summary>
    [GenerateSerializer]
    public class InstanceInfo
    {
        [Id(0)] public InstanceId InstanceId { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public WorldId WorldId { get; set; }
        [Id(3)] public string DungeonName { get; set; } = string.Empty;
        [Id(4)] public InstanceState State { get; set; }
        [Id(5)] public int PlayerCount { get; set; }
        [Id(6)] public int MaxPlayers { get; set; }
        [Id(7)] public DateTime CreatedAt { get; set; }
        [Id(8)] public DateTime? LastActivityAt { get; set; }
        [Id(9)] public PartyId? PartyId { get; set; }
        [Id(10)] public List<PlayerId> PlayerIds { get; set; } = new List<PlayerId>();
        [Id(11)] public string? MapId { get; set; } // Map grain ID for this instance
        [Id(12)] public LockoutKey? LockoutKey { get; set; }
    }
}

