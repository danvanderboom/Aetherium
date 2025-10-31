using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain interface for a single game map within a world.
    /// Each map represents a region, floor, district, etc.
    /// </summary>
    public interface IGameMapGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the map with a generated world.
        /// </summary>
        Task InitializeAsync(string worldId, string mapName, WorldSize size, string generatorType, Dictionary<string, object> parameters);

        /// <summary>
        /// Gets the current world state for this map.
        /// </summary>
        Task<string?> GetWorldAsync();

        /// <summary>
        /// Gets map metadata.
        /// </summary>
        Task<MapMetadata?> GetMetadataAsync();

        /// <summary>
        /// Adds a player to this map.
        /// </summary>
        Task<bool> AddPlayerAsync(string playerId);

        /// <summary>
        /// Removes a player from this map.
        /// </summary>
        Task RemovePlayerAsync(string playerId);

        /// <summary>
        /// Gets all players currently in this map.
        /// </summary>
        Task<List<string>> GetPlayersAsync();

        /// <summary>
        /// Processes a game tick for this map (NPC movement, etc.).
        /// </summary>
        /// <param name="gameTimeElapsed">Elapsed game time for this tick</param>
        Task TickAsync(TimeSpan gameTimeElapsed);

        /// <summary>
        /// Saves all regions in this map to persistent storage.
        /// </summary>
        Task SaveMapAsync();

        /// <summary>
        /// Loads map regions from persistent storage.
        /// </summary>
        Task<bool> LoadMapAsync();

        /// <summary>
        /// Spawns an entity in the world at the specified location.
        /// </summary>
        Task<SpawnEntityResult> SpawnEntityAsync(SpawnEntityRequest request);

        /// <summary>
        /// Builds a structure in the world at the specified location.
        /// </summary>
        Task<BuildStructureResult> BuildStructureAsync(BuildStructureRequest request);
    }

    /// <summary>
    /// Metadata about a game map.
    /// </summary>
    [GenerateSerializer]
    public class MapMetadata
    {
        [Id(0)] public string MapId { get; set; } = string.Empty;
        [Id(1)] public string WorldId { get; set; } = string.Empty;
        [Id(2)] public string MapName { get; set; } = string.Empty;
        [Id(3)] public WorldSize Size { get; set; } = new WorldSize();
        [Id(4)] public string GeneratorType { get; set; } = string.Empty;
        [Id(5)] public int PlayerCount { get; set; }
        [Id(6)] public System.DateTime CreatedAt { get; set; }
    }
}


