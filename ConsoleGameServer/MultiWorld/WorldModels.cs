using System;
using System.Collections.Generic;

namespace ConsoleGameServer.MultiWorld
{
    /// <summary>
    /// Configuration for creating a new game world.
    /// </summary>
    public class WorldConfig
    {
        public string WorldId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GeneratorType { get; set; } = "rooms-and-corridors"; // Generator to use
        public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        public string? NarrativeId { get; set; } // Optional narrative
        public int MaxPlayers { get; set; } = 100;
        public WorldSize Size { get; set; } = new WorldSize { Width = 100, Height = 100, Depth = 1 };
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "system";
    }

    /// <summary>
    /// Size dimensions for a world.
    /// </summary>
    public class WorldSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; } // Number of Z-levels
    }

    /// <summary>
    /// Current state of a running world.
    /// </summary>
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
    public class WorldInfo
    {
        public string WorldId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorldState State { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public string? NarrativeId { get; set; }
        public List<string> MapIds { get; set; } = new List<string>(); // IDs of GameMapGrains
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Request to create a new world.
    /// </summary>
    public class CreateWorldRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GeneratorType { get; set; } = "rooms-and-corridors";
        public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        public string? NarrativeId { get; set; }
        public int MaxPlayers { get; set; } = 100;
        public WorldSize? Size { get; set; }
    }

    /// <summary>
    /// Result of world creation.
    /// </summary>
    public class CreateWorldResult
    {
        public bool Success { get; set; }
        public string? WorldId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

