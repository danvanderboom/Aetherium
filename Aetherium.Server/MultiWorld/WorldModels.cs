using System;
using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;

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


