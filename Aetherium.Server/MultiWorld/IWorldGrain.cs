using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain interface for coordinating a multi-map world.
    /// Each world can have multiple map grains (floors, regions, districts, etc.).
    /// </summary>
    public interface IWorldGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the world with configuration.
        /// </summary>
        Task InitializeAsync(WorldConfig config);

        /// <summary>
        /// Gets world information.
        /// </summary>
        Task<WorldInfo?> GetInfoAsync();

        /// <summary>
        /// Gets the current world state.
        /// </summary>
        Task<WorldState> GetStateAsync();

        /// <summary>
        /// Pauses the world (stops ticks, prevents new players).
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Resumes a paused world.
        /// </summary>
        Task ResumeAsync();

        /// <summary>
        /// Shuts down and removes the world.
        /// </summary>
        Task ShutdownAsync();

        /// <summary>
        /// Adds a new map to the world.
        /// </summary>
        Task<string> AddMapAsync(string mapName, string generatorType, Dictionary<string, object> parameters);

        /// <summary>
        /// Gets all map IDs in this world.
        /// </summary>
        Task<List<string>> GetMapIdsAsync();

        /// <summary>
        /// Adds a player to the world (default map or specified map).
        /// </summary>
        Task<bool> AddPlayerAsync(string playerId, string? mapId = null);

        /// <summary>
        /// Removes a player from the world.
        /// </summary>
        Task RemovePlayerAsync(string playerId);

        /// <summary>
        /// Moves a player between maps within this world.
        /// </summary>
        Task<bool> MovePlayerToMapAsync(string playerId, string targetMapId);

        /// <summary>
        /// Gets the current map ID for a player.
        /// </summary>
        Task<string?> GetPlayerMapAsync(string playerId);

        /// <summary>
        /// Processes a world tick (delegates to map grains).
        /// </summary>
        Task TickAsync();

        /// <summary>
        /// Saves the current world state (all regions) to persistent storage.
        /// </summary>
        Task SaveWorldAsync();

        /// <summary>
        /// Loads a world from persistent storage.
        /// </summary>
        Task<bool> LoadWorldAsync();
    }
}


