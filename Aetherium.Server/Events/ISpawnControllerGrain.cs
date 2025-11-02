using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain interface for managing entity spawns during events.
    /// Keyed by event instance ID.
    /// </summary>
    public interface ISpawnControllerGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Spawns entities for an event at a location.
        /// </summary>
        Task<SpawnResult> SpawnEntitiesAsync(
            string eventType,
            Dictionary<string, object> spawnConfig,
            string mapId,
            int x, int y, int z,
            int count);

        /// <summary>
        /// Despawns entities for an event.
        /// </summary>
        Task<bool> DespawnEntitiesAsync(List<string> entityIds);

        /// <summary>
        /// Gets all entities spawned for this event.
        /// </summary>
        Task<List<string>> GetSpawnedEntitiesAsync();
    }

    /// <summary>
    /// Result of a spawn operation.
    /// </summary>
    [GenerateSerializer]
    public class SpawnResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public List<string> EntityIds { get; set; } = new List<string>();
        [Id(2)] public string? ErrorMessage { get; set; }
    }
}

