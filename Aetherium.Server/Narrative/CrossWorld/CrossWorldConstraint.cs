using System.Collections.Generic;
using Orleans;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative.CrossWorld
{
    /// <summary>
    /// Constraint that requires a quest objective to be completed in a different world.
    /// </summary>
    [GenerateSerializer]
    public class CrossWorldConstraint
    {
        [Id(0)] public WorldSelector? WorldSelector { get; set; }
        [Id(1)] public MapSelector? MapSelector { get; set; }
        [Id(2)] public bool RequiresUnlock { get; set; }
    }

    /// <summary>
    /// Selector for choosing a target world within a cluster.
    /// </summary>
    [GenerateSerializer]
    public class WorldSelector
    {
        [Id(0)] public string? WorldId { get; set; } // Exact world ID
        [Id(1)] public string? WorldTag { get; set; } // Tag to match (e.g., "hub", "city")
        [Id(2)] public string? WorldTemplate { get; set; } // Template name to match
        [Id(3)] public List<string> ExcludeWorldIds { get; set; } = new List<string>(); // Worlds to exclude
    }

    /// <summary>
    /// Selector for choosing a target map within a world.
    /// </summary>
    [GenerateSerializer]
    public class MapSelector
    {
        [Id(0)] public string? MapId { get; set; } // Exact map ID
        [Id(1)] public string? MapTag { get; set; } // Tag to match (e.g., "entrance", "boss")
        [Id(2)] public string? MapName { get; set; } // Map name to match
    }
}

