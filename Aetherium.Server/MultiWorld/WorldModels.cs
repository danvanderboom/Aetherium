using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Model.Progression;
using Aetherium.Model.Factions;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Configuration for creating a new game world.
    /// </summary>
    [GenerateSerializer]
    public class WorldConfig
    {
        [Id(0)] public string WorldId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public string GeneratorType { get; set; } = "rooms-and-corridors"; // Generator to use
        [Id(4)] public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        [Id(5)] public string? NarrativeId { get; set; } // Optional narrative
        [Id(6)] public int MaxPlayers { get; set; } = 100;
        [Id(7)] public WorldSize Size { get; set; } = new WorldSize { Width = 100, Height = 100, Depth = 1 };
        [Id(8)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Id(9)] public string CreatedBy { get; set; } = "system";
        [Id(10)] public string NarrativeStateScope { get; set; } = "shared"; // "shared" or "per-world"
        [Id(11)] public int? NarrativeSeed { get; set; } // Optional seed for deterministic narrative generation
        [Id(12)] public string? ClusterId { get; set; } // Optional cluster ID for multi-world ecosystems

        /// <summary>Per-world death/respawn rules (engine gap-analysis §4.11). Null means every map
        /// on this world falls back to <see cref="DeathPolicy.Default"/> — see wire-death-respawn-live.</summary>
        [Id(13)] public DeathPolicy? DeathPolicy { get; set; }

        /// <summary>Per-world ability content (engine gap-analysis §4.3): abilities available on this
        /// world's maps and the resource pools its characters start with. Null means no abilities. See
        /// wire-abilities-live.</summary>
        [Id(14)] public AbilityConfig? AbilityConfig { get; set; }

        /// <summary>Per-world character-progression content (engine gap-analysis §4.4). Null means no
        /// progression. See wire-progression-live.</summary>
        [Id(15)] public ProgressionConfig? ProgressionConfig { get; set; }

        /// <summary>Per-world faction content (engine gap-analysis §4.6). Null means no factions.
        /// See wire-factions-live.</summary>
        [Id(16)] public FactionConfig? FactionConfig { get; set; }

        /// <summary>Id of the game definition this world was created from, if any
        /// (add-game-definition-loader). Null for worlds created outside the definition path.</summary>
        [Id(17)] public string? GameDefinitionId { get; set; }

        /// <summary>Version of the game definition this world was created from, if any.</summary>
        [Id(18)] public string? GameDefinitionVersion { get; set; }

        /// <summary>Per-world content vocabulary (add-content-definitions): creatures, items, spawn
        /// mix. Null preserves the legacy hardcoded population exactly.</summary>
        [Id(19)] public Aetherium.Model.Content.ContentConfig? ContentConfig { get; set; }

        /// <summary>Per-world reactive logic (add-eca-scripting): event–condition–action rules.</summary>
        [Id(20)] public Aetherium.Model.Eca.EcaConfig? EcaConfig { get; set; }

        /// <summary>The world's tiling (docs/grid-topologies.md): "square" (default) | "hex" | "tri"
        /// | (later) "h3". Null/empty means square, byte-identically to the pre-topology engine.</summary>
        [Id(21)] public string? Topology { get; set; }
    }

    /// <summary>
    /// Size dimensions for a world.
    /// </summary>
    [GenerateSerializer]
    public class WorldSize
    {
        [Id(0)] public int Width { get; set; }
        [Id(1)] public int Height { get; set; }
        [Id(2)] public int Depth { get; set; } // Number of Z-levels
    }

    /// <summary>
    /// Current state of a running world.
    /// </summary>
    [GenerateSerializer]
    public enum WorldState
    {
        Creating,
        Active,
        Paused,
        ShuttingDown,
        Stopped
    }

    /// <summary>
    /// Information about a world instance.
    /// </summary>
    [GenerateSerializer]
    public class WorldInfo
    {
        [Id(0)] public string WorldId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public WorldState State { get; set; }
        [Id(4)] public int PlayerCount { get; set; }
        [Id(5)] public int MaxPlayers { get; set; }
        [Id(6)] public DateTime CreatedAt { get; set; }
        [Id(7)] public DateTime? LastActivityAt { get; set; }
        [Id(8)] public string? NarrativeId { get; set; }
        [Id(9)] public List<string> MapIds { get; set; } = new List<string>(); // IDs of GameMapGrains
        [Id(10)] public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        [Id(11)] public string? ClusterId { get; set; } // Cluster ID for multi-world ecosystems

        /// <summary>Id of the game definition this world was created from, if any
        /// (add-game-definition-loader) — lets instance listings group worlds by game.</summary>
        [Id(12)] public string? GameDefinitionId { get; set; }

        /// <summary>Version of the game definition this world was created from, if any.</summary>
        [Id(13)] public string? GameDefinitionVersion { get; set; }
    }

    /// <summary>
    /// Request to create a new world.
    /// </summary>
    [GenerateSerializer]
    public class CreateWorldRequest
    {
        [Id(0)] public string Name { get; set; } = string.Empty;
        [Id(1)] public string Description { get; set; } = string.Empty;
        [Id(2)] public string GeneratorType { get; set; } = "rooms-and-corridors";
        [Id(3)] public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        [Id(4)] public string? NarrativeId { get; set; }
        [Id(5)] public int MaxPlayers { get; set; } = 100;
        [Id(6)] public WorldSize? Size { get; set; }
        [Id(7)] public string? ClusterId { get; set; } // Optional cluster ID for multi-world ecosystems

        /// <summary>Per-world death/respawn rules (engine gap-analysis §4.11). Null means every map
        /// on this world falls back to <see cref="DeathPolicy.Default"/> — see wire-death-respawn-live.</summary>
        [Id(8)] public DeathPolicy? DeathPolicy { get; set; }

        /// <summary>Per-world ability content (engine gap-analysis §4.3): abilities available on this
        /// world's maps and the resource pools its characters start with. Null means no abilities. See
        /// wire-abilities-live.</summary>
        [Id(9)] public AbilityConfig? AbilityConfig { get; set; }

        /// <summary>Per-world character-progression content (engine gap-analysis §4.4). Null means no
        /// progression. See wire-progression-live.</summary>
        [Id(10)] public ProgressionConfig? ProgressionConfig { get; set; }

        /// <summary>Per-world faction content (engine gap-analysis §4.6). Null means no factions.
        /// See wire-factions-live.</summary>
        [Id(11)] public FactionConfig? FactionConfig { get; set; }

        /// <summary>Id of the game definition this world was created from, if any
        /// (add-game-definition-loader). Set by the definition→instance path; null elsewhere.</summary>
        [Id(12)] public string? GameDefinitionId { get; set; }

        /// <summary>Version of the game definition this world was created from, if any.</summary>
        [Id(13)] public string? GameDefinitionVersion { get; set; }

        /// <summary>Per-world content vocabulary (add-content-definitions): creatures, items, spawn
        /// mix. Null preserves the legacy hardcoded population exactly.</summary>
        [Id(14)] public Aetherium.Model.Content.ContentConfig? ContentConfig { get; set; }

        /// <summary>Per-world reactive logic (add-eca-scripting): event–condition–action rules.</summary>
        [Id(15)] public Aetherium.Model.Eca.EcaConfig? EcaConfig { get; set; }

        /// <summary>The world's tiling (docs/grid-topologies.md): "square" (default) | "hex" | "tri"
        /// | (later) "h3". Null/empty means square, byte-identically to the pre-topology engine.</summary>
        [Id(16)] public string? Topology { get; set; }
    }

    /// <summary>
    /// Result of world creation.
    /// </summary>
    [GenerateSerializer]
    public class CreateWorldResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? WorldId { get; set; }
        [Id(2)] public string? ErrorMessage { get; set; }
    }
}


