using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;

namespace Aetherium.Model.Worlds
{
    /// <summary>
    /// Unique identifier for a world instance.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct WorldId(string Value);

    /// <summary>
    /// Unique identifier for an instance (dungeon/raid).
    /// </summary>
    [GenerateSerializer]
    public readonly record struct InstanceId(string Value);

    /// <summary>
    /// Unique identifier for a player.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct PlayerId(string Value);

    /// <summary>
    /// Unique identifier for a party.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct PartyId(string Value);

    /// <summary>
    /// Unique identifier for an invite.
    /// </summary>
    [GenerateSerializer]
    public readonly record struct InviteId(string Value);

    /// <summary>
    /// World host kind (authoritative server vs peer-hosted).
    /// </summary>
    [GenerateSerializer]
    public enum WorldHostKind
    {
        AuthoritativeServer,
        PeerHostedSession
    }

    /// <summary>
    /// Access control level for a world.
    /// </summary>
    [GenerateSerializer]
    public enum WorldAccessLevel
    {
        Public,
        Private
    }

    /// <summary>
    /// Access control list for a world.
    /// </summary>
    [GenerateSerializer]
    public class WorldAcl
    {
        [Id(0)] public WorldAccessLevel AccessLevel { get; set; } = WorldAccessLevel.Public;
        [Id(1)] public HashSet<PlayerId> AllowedPlayers { get; set; } = new HashSet<PlayerId>();
        [Id(2)] public HashSet<PlayerId> OwnerPlayers { get; set; } = new HashSet<PlayerId>();
    }

    /// <summary>
    /// Template for creating a world.
    /// </summary>
    [GenerateSerializer]
    public class WorldTemplate
    {
        [Id(0)] public string Name { get; set; } = string.Empty;
        [Id(1)] public string Description { get; set; } = string.Empty;
        [Id(2)] public string GeneratorType { get; set; } = "rooms-and-corridors";
        [Id(3)] public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        [Id(4)] public int MaxPlayers { get; set; } = 100;
        [Id(5)] public string? NarrativeId { get; set; }
        [Id(6)] public string? ClusterId { get; set; }

        /// <summary>Per-world death/respawn rules (engine gap-analysis §4.11). Null means every map
        /// on this world falls back to <see cref="DeathPolicy.Default"/> — see wire-death-respawn-live.</summary>
        [Id(7)] public DeathPolicy? DeathPolicy { get; set; }

        /// <summary>Per-world ability content (engine gap-analysis §4.3): the abilities available on
        /// this world's maps and the resource pools its characters start with. Null means no abilities
        /// — the engine ships none. See wire-abilities-live.</summary>
        [Id(8)] public AbilityConfig? AbilityConfig { get; set; }
    }

    /// <summary>
    /// Summary of a world for listing.
    /// </summary>
    [GenerateSerializer]
    public class WorldSummary
    {
        [Id(0)] public WorldId WorldId { get; set; }
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public WorldAccessLevel AccessLevel { get; set; }
        [Id(3)] public int PlayerCount { get; set; }
        [Id(4)] public int MaxPlayers { get; set; }
        [Id(5)] public DateTime CreatedAt { get; set; }
        [Id(6)] public DateTime? LastActivityAt { get; set; }
    }

    /// <summary>
    /// Query for listing worlds.
    /// </summary>
    [GenerateSerializer]
    public class WorldQuery
    {
        [Id(0)] public WorldAccessLevel? AccessLevel { get; set; }
        [Id(1)] public PlayerId? PlayerId { get; set; } // For filtering accessible worlds
        [Id(2)] public int? MaxResults { get; set; }
    }

    /// <summary>
    /// World event from streams.
    /// </summary>
    [GenerateSerializer]
    public class WorldEvent
    {
        [Id(0)] public WorldId WorldId { get; set; }
        [Id(1)] public string EventType { get; set; } = string.Empty;
        [Id(2)] public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        [Id(3)] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Stream identifier for world events.
    /// </summary>
    [GenerateSerializer]
    public enum WorldStream
    {
        Events,
        Chat,
        Zone
    }

    /// <summary>
    /// Invite information.
    /// </summary>
    [GenerateSerializer]
    public class WorldInvite
    {
        [Id(0)] public InviteId InviteId { get; set; }
        [Id(1)] public WorldId WorldId { get; set; }
        [Id(2)] public PlayerId InvitedBy { get; set; }
        [Id(3)] public PlayerId InvitedPlayer { get; set; }
        [Id(4)] public DateTime CreatedAt { get; set; }
        [Id(5)] public DateTime? ExpiresAt { get; set; }
        [Id(6)] public bool Accepted { get; set; }
    }
}

