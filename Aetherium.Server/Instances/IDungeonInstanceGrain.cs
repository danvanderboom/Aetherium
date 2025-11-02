using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain interface for managing a single dungeon instance.
    /// Keyed by instance ID.
    /// </summary>
    public interface IDungeonInstanceGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the instance with dungeon configuration.
        /// </summary>
        Task InitializeAsync(InstanceConfig config);

        /// <summary>
        /// Gets instance information.
        /// </summary>
        Task<InstanceInfo?> GetInfoAsync();

        /// <summary>
        /// Adds players to the instance.
        /// </summary>
        Task<bool> AddPlayersAsync(List<PlayerId> playerIds);

        /// <summary>
        /// Removes a player from the instance.
        /// </summary>
        Task RemovePlayerAsync(PlayerId playerId);

        /// <summary>
        /// Gets all players currently in the instance.
        /// </summary>
        Task<List<PlayerId>> GetPlayersAsync();

        /// <summary>
        /// Checks if a player is in the instance.
        /// </summary>
        Task<bool> IsPlayerInInstanceAsync(PlayerId playerId);

        /// <summary>
        /// Processes a game tick for this instance.
        /// </summary>
        Task TickAsync(TimeSpan gameTimeElapsed);

        /// <summary>
        /// Gets the world/map ID for this instance (used for player teleportation).
        /// </summary>
        Task<string?> GetMapIdAsync();

        /// <summary>
        /// Shuts down the instance and releases resources.
        /// </summary>
        Task ShutdownAsync();
    }
}

