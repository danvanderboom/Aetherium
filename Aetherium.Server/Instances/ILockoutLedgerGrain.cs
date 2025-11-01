using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain managing lockout entries for dungeons/instances.
    /// Keyed by dungeon ID.
    /// </summary>
    public interface ILockoutLedgerGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Checks if a party/player can enter a dungeon instance.
        /// </summary>
        Task<LockoutCheckResult> CheckLockoutAsync(PartyId? partyId, List<PlayerId> playerIds);

        /// <summary>
        /// Records a lockout entry after entering an instance.
        /// </summary>
        Task<LockoutKey> RecordLockoutAsync(PartyId? partyId, List<PlayerId> playerIds, InstanceId instanceId);

        /// <summary>
        /// Clears a lockout entry (admin function or after completion).
        /// </summary>
        Task<bool> ClearLockoutAsync(LockoutKey lockoutKey);

        /// <summary>
        /// Gets all active lockouts for a player.
        /// </summary>
        Task<List<LockoutEntry>> GetPlayerLockoutsAsync(PlayerId playerId);

        /// <summary>
        /// Gets all active lockouts for a party.
        /// </summary>
        Task<List<LockoutEntry>> GetPartyLockoutsAsync(PartyId partyId);

        /// <summary>
        /// Sets the lockout policy for this dungeon.
        /// </summary>
        Task SetPolicyAsync(LockoutPolicy policy);

        /// <summary>
        /// Gets the lockout policy for this dungeon.
        /// </summary>
        Task<LockoutPolicy?> GetPolicyAsync();
    }
}

