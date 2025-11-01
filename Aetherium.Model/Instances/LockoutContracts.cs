using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;

namespace Aetherium.Model.Instances
{
    /// <summary>
    /// Lockout key used to track instance access restrictions.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct LockoutKey(string Value);

    /// <summary>
    /// Unique identifier for a dungeon definition.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct DungeonId(string Value);

    /// <summary>
    /// Lockout policy for a dungeon/instance.
    /// </summary>
    [GenerateSerializer]
    public class LockoutPolicy
    {
        [Id(0)] public DungeonId DungeonId { get; set; }
        [Id(1)] public LockoutType Type { get; set; } = LockoutType.TimeBased;
        [Id(2)] public TimeSpan Duration { get; set; } = TimeSpan.FromHours(24);
        [Id(3)] public int MaxAttempts { get; set; } = -1; // -1 = unlimited
        [Id(4)] public bool ResetOnSuccess { get; set; } = false;
    }

    /// <summary>
    /// Type of lockout mechanism.
    /// </summary>
    [GenerateSerializer]
    public enum LockoutType
    {
        TimeBased,      // Lockout expires after duration
        AttemptBased,   // Lockout after max attempts
        Hybrid          // Both time and attempts
    }

    /// <summary>
    /// Lockout entry for a player or party.
    /// </summary>
    [GenerateSerializer]
    public class LockoutEntry
    {
        [Id(0)] public LockoutKey LockoutKey { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public PartyId? PartyId { get; set; }
        [Id(3)] public PlayerId? PlayerId { get; set; }
        [Id(4)] public DateTime LockoutUntil { get; set; }
        [Id(5)] public int AttemptsUsed { get; set; }
        [Id(6)] public bool IsLocked { get; set; }
        [Id(7)] public DateTime LastAttemptAt { get; set; }
        [Id(8)] public InstanceId? InstanceId { get; set; } // Current active instance
    }

    /// <summary>
    /// Result of checking lockout status.
    /// </summary>
    [GenerateSerializer]
    public class LockoutCheckResult
    {
        [Id(0)] public bool CanEnter { get; set; }
        [Id(1)] public string? Reason { get; set; }
        [Id(2)] public DateTime? LockoutUntil { get; set; }
        [Id(3)] public LockoutKey? LockoutKey { get; set; }
    }

    /// <summary>
    /// Request to enter an instance.
    /// </summary>
    [GenerateSerializer]
    public class EnterInstanceRequest
    {
        [Id(0)] public WorldId WorldId { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public PartyId? PartyId { get; set; }
        [Id(3)] public List<PlayerId> PlayerIds { get; set; } = new List<PlayerId>();
    }

    /// <summary>
    /// Result of entering an instance.
    /// </summary>
    [GenerateSerializer]
    public class EnterInstanceResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public InstanceId? InstanceId { get; set; }
        [Id(2)] public LockoutKey? LockoutKey { get; set; }
        [Id(3)] public string? ErrorMessage { get; set; }
    }
}

