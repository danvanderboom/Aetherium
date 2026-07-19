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
        /// Removes a map from the world so it is no longer ticked, saved, or loaded, and drops any
        /// player locations that pointed at it. Used to free abandoned dungeon-instance maps.
        /// Returns true if the map was present and removed.
        /// </summary>
        Task<bool> RemoveMapAsync(string mapId);

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
        /// Records that <paramref name="playerId"/> now resides on <paramref name="mapId"/> in this
        /// world, keeping the world grain's player-location record (the "which map is this player on"
        /// source of truth) in agreement with a re-point performed directly against the map grains
        /// (see <c>add-boardable-vehicles</c> Phase 0). Unlike <see cref="AddPlayerAsync"/> /
        /// <see cref="MovePlayerToMapAsync"/>, this does NOT touch map-grain membership — the caller
        /// has already joined the target map (via <c>IGameMapGrain.JoinPlayerAsync</c>) and left the
        /// old one; this only updates the location index. Player count is incremented only when the
        /// player was not previously tracked, so repeated re-points don't inflate it.
        /// </summary>
        Task RegisterPlayerLocationAsync(string playerId, string mapId);

        /// <summary>
        /// Drops <paramref name="playerId"/> from this world's player-location record when a re-point
        /// moves them to a different world (see <c>add-boardable-vehicles</c> Phase 0). Pairs with
        /// <see cref="RegisterPlayerLocationAsync"/> on the destination world. Does NOT touch map-grain
        /// membership — the caller has already left the old map via <c>IGameMapGrain.LeavePlayerAsync</c>
        /// (which strips the Character and frees the spawn); this only updates the location index and
        /// decrements the player count. No-op when the player was not tracked here.
        /// </summary>
        Task UnregisterPlayerAsync(string playerId);

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


