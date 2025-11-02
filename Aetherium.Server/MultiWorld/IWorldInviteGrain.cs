using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing invites for a world.
    /// </summary>
    public interface IWorldInviteGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Creates an invite for a player.
        /// </summary>
        Task<InviteId> CreateInviteAsync(PlayerId invitedBy, PlayerId invitedPlayer, System.TimeSpan? expiry = null);

        /// <summary>
        /// Gets an invite by ID.
        /// </summary>
        Task<WorldInvite?> GetInviteAsync(InviteId inviteId);

        /// <summary>
        /// Accepts an invite.
        /// </summary>
        Task<bool> AcceptInviteAsync(InviteId inviteId);

        /// <summary>
        /// Lists pending invites for a player.
        /// </summary>
        Task<IReadOnlyList<WorldInvite>> GetPendingInvitesAsync(PlayerId playerId);
    }
}

