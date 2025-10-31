using System;
using Orleans;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Serializable request to spawn an entity in the world.
    /// </summary>
    [GenerateSerializer]
    public class SpawnEntityRequest
    {
        [Id(0)] public string CreatureType { get; set; } = string.Empty;
        [Id(1)] public int X { get; set; }
        [Id(2)] public int Y { get; set; }
        [Id(3)] public int Z { get; set; }
        [Id(4)] public double SpawnRate { get; set; }
    }

    /// <summary>
    /// Serializable request to build a structure in the world.
    /// </summary>
    [GenerateSerializer]
    public class BuildStructureRequest
    {
        [Id(0)] public string PrefabId { get; set; } = string.Empty;
        [Id(1)] public int X { get; set; }
        [Id(2)] public int Y { get; set; }
        [Id(3)] public int Z { get; set; }
        [Id(4)] public int Rotation { get; set; }
    }

    /// <summary>
    /// Result of a spawn operation.
    /// </summary>
    [GenerateSerializer]
    public class SpawnEntityResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? EntityId { get; set; }
        [Id(2)] public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of a build operation.
    /// </summary>
    [GenerateSerializer]
    public class BuildStructureResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? ErrorMessage { get; set; }
    }
}

