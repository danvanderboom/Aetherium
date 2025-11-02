using Orleans;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing access control for a world.
    /// </summary>
    public interface IWorldAclGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Gets the ACL for this world.
        /// </summary>
        Task<WorldAcl> GetAclAsync();

        /// <summary>
        /// Sets the ACL for this world.
        /// </summary>
        Task SetAclAsync(WorldAcl acl);

        /// <summary>
        /// Checks if a player can access this world.
        /// </summary>
        Task<bool> CanAccessAsync(PlayerId playerId);

        /// <summary>
        /// Adds a player to the allowed list.
        /// </summary>
        Task AddPlayerAsync(PlayerId playerId);

        /// <summary>
        /// Removes a player from the allowed list.
        /// </summary>
        Task RemovePlayerAsync(PlayerId playerId);
    }
}

