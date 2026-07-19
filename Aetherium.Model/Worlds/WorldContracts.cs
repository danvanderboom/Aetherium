using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Model.Progression;
using Aetherium.Model.Factions;

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
    /// World dimensions as Model-layer data (the server's <c>WorldSize</c> is a server type, so
    /// contracts that live here — <see cref="WorldTemplate"/>, game definitions — carry this
    /// instead; the server maps it onto <c>WorldSize</c> at creation time).
    /// </summary>
    [GenerateSerializer]
    public class WorldDimensions
    {
        [Id(0)] public int Width { get; set; }
        [Id(1)] public int Height { get; set; }
        [Id(2)] public int Depth { get; set; } = 1;
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

        /// <summary>Per-world character-progression content (engine gap-analysis §4.4): XP pools,
        /// skills, starting attributes, XP-award and attribute-derivation rules. Null means no
        /// progression — the engine ships none. See wire-progression-live.</summary>
        [Id(9)] public ProgressionConfig? ProgressionConfig { get; set; }

        /// <summary>Per-world faction content (engine gap-analysis §4.6): factions with doctrines
        /// and rank rules, inter-faction relations, standing bands. Null means no factions — the
        /// engine ships none. See wire-factions-live and docs/factions-reputation.md.</summary>
        [Id(10)] public FactionConfig? FactionConfig { get; set; }

        /// <summary>Requested world dimensions. Null falls back to the server default. (Added with
        /// add-game-definition-loader; previously the IWorldHost creation path silently dropped the
        /// requested size because this contract had no field to carry it.)</summary>
        [Id(11)] public WorldDimensions? Size { get; set; }

        /// <summary>Id of the game definition this world was created from, if any
        /// (add-game-definition-loader). Null for worlds created outside the definition path.</summary>
        [Id(12)] public string? GameDefinitionId { get; set; }

        /// <summary>Version of the game definition this world was created from, if any.</summary>
        [Id(13)] public string? GameDefinitionVersion { get; set; }

        /// <summary>Per-world content vocabulary (add-content-definitions): creatures, items, and
        /// spawn mix. Null preserves the legacy hardcoded population exactly.</summary>
        [Id(14)] public Aetherium.Model.Content.ContentConfig? ContentConfig { get; set; }

        /// <summary>Per-world reactive logic (add-eca-scripting): event–condition–action rules. Null
        /// means no rules fire.</summary>
        [Id(15)] public Aetherium.Model.Eca.EcaConfig? EcaConfig { get; set; }

        /// <summary>The world's tiling (docs/grid-topologies.md): "square" (default) | "hex" | "tri"
        /// | (later) "h3". Null/empty means square, byte-identically to the pre-topology engine.</summary>
        [Id(16)] public string? Topology { get; set; }

        /// <summary>Per-world economy recipe (goods/prices/basket/biome production). Null → engine default.</summary>
        [Id(17)] public Aetherium.Model.Economy.EconomyConfig? EconomyConfig { get; set; }
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

