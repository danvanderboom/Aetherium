using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain interface for allocating dungeon instances for parties/players.
    /// Keyed by world ID.
    /// </summary>
    public interface IInstanceAllocatorGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Enters an instance for a party or group of players.
        /// Checks lockouts, allocates/reuses instances, and returns instance ID.
        /// </summary>
        Task<EnterInstanceResult> EnterAsync(EnterInstanceRequest request);

        /// <summary>
        /// Allocates a new instance for a dungeon (used internally).
        /// </summary>
        Task<InstanceId> AllocateInstanceAsync(DungeonId dungeonId, PartyId? partyId, List<PlayerId> playerIds);

        /// <summary>
        /// Gets or reuses an existing instance for a party/player (if already in one).
        /// </summary>
        Task<InstanceId?> GetOrReuseInstanceAsync(DungeonId dungeonId, PartyId? partyId, List<PlayerId> playerIds);

        /// <summary>
        /// Releases an instance when it's no longer needed.
        /// </summary>
        Task ReleaseInstanceAsync(InstanceId instanceId);
    }
}

