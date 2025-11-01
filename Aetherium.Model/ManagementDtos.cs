using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// DTO for session information exposed via API.
    /// </summary>
    [GenerateSerializer]
    public class SessionInfoDto
    {
        [Id(0)]
        public string SessionId { get; set; } = string.Empty;
        
        [Id(1)]
        public string ConnectionId { get; set; } = string.Empty;
        
        [Id(2)]
        public bool DirectionalVisionMode { get; set; }
        
        [Id(3)]
        public int HeadingDegrees { get; set; }
        
        [Id(4)]
        public int FieldOfViewDegrees { get; set; }
        
        [Id(5)]
        public LightingMode LightingMode { get; set; }
        
        [Id(6)]
        public VisionMode VisionMode { get; set; }
        
        [Id(7)]
        public double TimeScale { get; set; }
        
        [Id(8)]
        public DateTime ConnectedAt { get; set; }
        
        [Id(9)]
        public string? WorldId { get; set; }
        
        [Id(10)]
        public List<string> AttachedAgentIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for world information exposed via API.
    /// </summary>
    [GenerateSerializer]
    public class WorldInfoDto
    {
        [Id(0)]
        public string WorldId { get; set; } = string.Empty;
        
        [Id(1)]
        public string Name { get; set; } = string.Empty;
        
        [Id(2)]
        public string Description { get; set; } = string.Empty;
        
        [Id(3)]
        public string State { get; set; } = "Active"; // "Creating", "Active", "Paused", "ShuttingDown", "Stopped"
        
        [Id(4)]
        public int PlayerCount { get; set; }
        
        [Id(5)]
        public int MaxPlayers { get; set; }
        
        [Id(6)]
        public DateTime CreatedAt { get; set; }
        
        [Id(7)]
        public DateTime? LastActivityAt { get; set; }
        
        [Id(8)]
        public string? NarrativeId { get; set; }
        
        [Id(9)]
        public List<string> MapIds { get; set; } = new List<string>();
        
        [Id(10)]
        public List<string> SessionIds { get; set; } = new List<string>(); // Active sessions in this world
        
        [Id(11)]
        public string? ClusterId { get; set; } // Cluster ID for multi-world ecosystems
    }

    /// <summary>
    /// DTO for agent information exposed via API.
    /// </summary>
    [GenerateSerializer]
    public class AgentInfoDto
    {
        [Id(0)]
        public string AgentId { get; set; } = string.Empty;
        
        [Id(1)]
        public string RunnerId { get; set; } = string.Empty;
        
        [Id(2)]
        public string? SessionId { get; set; }
        
        [Id(3)]
        public bool IsRunning { get; set; }
        
        [Id(4)]
        public int Steps { get; set; }
        
        [Id(5)]
        public string LastAction { get; set; } = string.Empty;
        
        [Id(6)]
        public string LastResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to create a new world.
    /// </summary>
    [GenerateSerializer]
    public class CreateWorldRequestDto
    {
        [Id(0)]
        public string Name { get; set; } = string.Empty;
        
        [Id(1)]
        public string Description { get; set; } = string.Empty;
        
        [Id(2)]
        public string GeneratorType { get; set; } = "rooms-and-corridors";
        
        [Id(3)]
        public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        
        [Id(4)]
        public string? NarrativeId { get; set; }
        
        [Id(5)]
        public int MaxPlayers { get; set; } = 100;
        
        [Id(6)]
        public WorldSizeDto? Size { get; set; }
    }

    /// <summary>
    /// World size dimensions.
    /// </summary>
    [GenerateSerializer]
    public class WorldSizeDto
    {
        [Id(0)]
        public int Width { get; set; }
        
        [Id(1)]
        public int Height { get; set; }
        
        [Id(2)]
        public int Depth { get; set; }
    }

    /// <summary>
    /// Request to create a new session.
    /// </summary>
    [GenerateSerializer]
    public class CreateSessionRequestDto
    {
        [Id(0)]
        public string? WorldId { get; set; } // If null, creates a new single-world session
    }

    /// <summary>
    /// Request to attach an agent to a session.
    /// </summary>
    [GenerateSerializer]
    public class AttachAgentRequestDto
    {
        [Id(0)]
        public string AgentId { get; set; } = string.Empty;
        
        [Id(1)]
        public string RunnerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to detach an agent from a session.
    /// </summary>
    [GenerateSerializer]
    public class DetachAgentRequestDto
    {
        [Id(0)]
        public string RunnerId { get; set; } = string.Empty;
    }
}

