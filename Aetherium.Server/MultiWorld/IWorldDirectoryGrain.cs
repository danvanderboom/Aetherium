using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain for managing world directory (listing, creating worlds).
    /// </summary>
    public interface IWorldDirectoryGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Registers a world in the directory.
        /// </summary>
        Task RegisterWorldAsync(WorldId worldId, WorldSummary summary);

        /// <summary>
        /// Removes a world from the directory.
        /// </summary>
        Task UnregisterWorldAsync(WorldId worldId);

        /// <summary>
        /// Lists worlds matching the query.
        /// </summary>
        Task<IReadOnlyList<WorldSummary>> ListWorldsAsync(WorldQuery query);

        /// <summary>
        /// Gets the default world ID (for bootstrapping).
        /// </summary>
        Task<WorldId?> GetDefaultWorldAsync();

        /// <summary>
        /// Sets the default world ID.
        /// </summary>
        Task SetDefaultWorldAsync(WorldId worldId);
    }
}

