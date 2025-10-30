using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConsoleGameServer.MultiWorld
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
        Task<ConsoleGame.Core.World?> GetWorldAsync();

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
        Task TickAsync();
    }

    /// <summary>
    /// Metadata about a game map.
    /// </summary>
    public class MapMetadata
    {
        public string MapId { get; set; } = string.Empty;
        public string WorldId { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public WorldSize Size { get; set; } = new WorldSize();
        public string GeneratorType { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}

