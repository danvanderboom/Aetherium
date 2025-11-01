using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Groups
{
    /// <summary>
    /// Orleans grain managing a party (up to 5 members).
    /// </summary>
    public interface IPartyGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Creates a new party with the leader.
        /// </summary>
        Task CreateAsync(PlayerId leaderId, string leaderName);

        /// <summary>
        /// Gets party information.
        /// </summary>
        Task<PartyInfo?> GetInfoAsync();

        /// <summary>
        /// Adds a player to the party.
        /// </summary>
        Task<bool> AddMemberAsync(PlayerId playerId, string playerName);

        /// <summary>
        /// Removes a player from the party.
        /// </summary>
        Task RemoveMemberAsync(PlayerId playerId);

        /// <summary>
        /// Sets the party leader.
        /// </summary>
        Task<bool> SetLeaderAsync(PlayerId newLeaderId);

        /// <summary>
        /// Checks if a player is in the party.
        /// </summary>
        Task<bool> IsMemberAsync(PlayerId playerId);

        /// <summary>
        /// Gets all member IDs.
        /// </summary>
        Task<List<PlayerId>> GetMemberIdsAsync();

        /// <summary>
        /// Updates a member's online status.
        /// </summary>
        Task UpdateMemberStatusAsync(PlayerId playerId, bool isOnline);
    }
}

