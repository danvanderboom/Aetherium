using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Groups
{
    /// <summary>
    /// Orleans grain managing a raid (up to 40 members).
    /// </summary>
    public interface IRaidGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Creates a new raid with the leader.
        /// </summary>
        Task CreateAsync(PlayerId leaderId, string leaderName);

        /// <summary>
        /// Gets raid information.
        /// </summary>
        Task<RaidInfo?> GetInfoAsync();

        /// <summary>
        /// Adds a player to the raid.
        /// </summary>
        Task<bool> AddMemberAsync(PlayerId playerId, string playerName);

        /// <summary>
        /// Removes a player from the raid.
        /// </summary>
        Task RemoveMemberAsync(PlayerId playerId);

        /// <summary>
        /// Sets the raid leader.
        /// </summary>
        Task<bool> SetLeaderAsync(PlayerId newLeaderId);

        /// <summary>
        /// Checks if a player is in the raid.
        /// </summary>
        Task<bool> IsMemberAsync(PlayerId playerId);

        /// <summary>
        /// Gets all member IDs.
        /// </summary>
        Task<List<PlayerId>> GetMemberIdsAsync();
    }
}

