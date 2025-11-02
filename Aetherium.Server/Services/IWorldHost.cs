using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Services
{
    /// <summary>
    /// Host abstraction for world management. Supports authoritative server (Orleans) 
    /// and future peer-hosted sessions.
    /// </summary>
    public interface IWorldHost
    {
        /// <summary>
        /// Creates a new world with the specified template and ACL.
        /// </summary>
        Task<WorldId> CreateWorldAsync(WorldTemplate template, WorldAcl acl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the access control list for a world.
        /// </summary>
        Task SetWorldAclAsync(WorldId worldId, WorldAcl acl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists worlds matching the query.
        /// </summary>
        Task<IReadOnlyList<WorldSummary>> ListWorldsAsync(WorldQuery query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an invite for a player to join a private world.
        /// </summary>
        Task<InviteId> InviteAsync(WorldId worldId, PlayerId playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Accepts an invite.
        /// </summary>
        Task<bool> AcceptInviteAsync(InviteId inviteId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to world events via Orleans Streams.
        /// </summary>
        IAsyncEnumerable<WorldEvent> SubscribeAsync(WorldId worldId, WorldStream stream, CancellationToken cancellationToken = default);
    }
}

