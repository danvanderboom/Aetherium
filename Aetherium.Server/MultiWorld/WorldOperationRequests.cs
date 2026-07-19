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

        /// <summary>
        /// When true — or when <see cref="CreatureType"/> is a known flyer — the spawned entity is airborne
        /// and receives a Flight component. Flying spawns are validated against altitude bands rather than
        /// ground passability, so they may be placed in open air (e.g. a satellite in orbit).
        /// </summary>
        [Id(5)] public bool Flies { get; set; }
        [Id(6)] public int MinBand { get; set; } = 1;
        [Id(7)] public int MaxBand { get; set; } = 5;
        [Id(8)] public bool CanLand { get; set; }
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

