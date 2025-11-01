using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Server.HubWorld
{
    /// <summary>
    /// Definition for a hub world that connects multiple procedural zones.
    /// </summary>
    [GenerateSerializer]
    public class HubDefinition
    {
        [Id(0)] public string HubId { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
        [Id(3)] public string GeneratorType { get; set; } = "hub"; // Generator to use for hub world
        [Id(4)] public Dictionary<string, object> GeneratorParameters { get; set; } = new Dictionary<string, object>();
        [Id(5)] public HubSize Size { get; set; } = new HubSize { Width = 200, Height = 200, Depth = 1 };
        [Id(6)] public List<string> Tags { get; set; } = new List<string>(); // Tags like "hub", "city", "central"
        [Id(7)] public string? NarrativeId { get; set; } // Optional narrative for hub
        [Id(8)] public List<PortalDefinition> Portals { get; set; } = new List<PortalDefinition>(); // Portal definitions for connecting to other worlds
        [Id(9)] public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(); // Additional metadata
    }

    /// <summary>
    /// Size dimensions for a hub world.
    /// </summary>
    [GenerateSerializer]
    public class HubSize
    {
        [Id(0)] public int Width { get; set; }
        [Id(1)] public int Height { get; set; }
        [Id(2)] public int Depth { get; set; } // Number of Z-levels
    }

    /// <summary>
    /// Definition for a portal in a hub world.
    /// </summary>
    [GenerateSerializer]
    public class PortalDefinition
    {
        [Id(0)] public string PortalId { get; set; } = string.Empty;
        [Id(1)] public string? TargetWorldTag { get; set; } // Tag of target world (e.g., "dungeon", "city")
        [Id(2)] public string? TargetWorldTemplate { get; set; } // Template of target world
        [Id(3)] public string? TargetMapTag { get; set; } // Tag of target map within world
        [Id(4)] public string? TargetMapName { get; set; } // Name of target map
        [Id(5)] public string? Activation { get; set; } // Activation requirement (e.g., "unlocked", quest ID)
        [Id(6)] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>(); // Portal-specific parameters
    }
}

