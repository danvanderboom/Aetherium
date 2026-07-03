using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing invites for a world.
    /// </summary>
    public class WorldInviteGrain : Grain, IWorldInviteGrain
    {
        private readonly IPersistentState<WorldInviteState> _state;

        public WorldInviteGrain(
            [PersistentState("invites", "worldStore")] IPersistentState<WorldInviteState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new WorldInviteState
                {
                    Invites = new Dictionary<string, WorldInvite>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        // Accepted invites older than this are history, not pending work; expired ones
        // are dead immediately. Purged on create (which writes state anyway) so the
        // dictionary doesn't accumulate every invite ever sent for the world.
        private static readonly TimeSpan AcceptedInviteRetention = TimeSpan.FromDays(7);

        public async Task<InviteId> CreateInviteAsync(PlayerId invitedBy, PlayerId invitedPlayer, TimeSpan? expiry = null)
        {
            PurgeDeadInvites();

            var inviteId = Guid.NewGuid();
            var invite = new WorldInvite
            {
                InviteId = new InviteId(inviteId.ToString()),
                WorldId = new WorldId(this.GetPrimaryKeyString()),
                InvitedBy = invitedBy,
                InvitedPlayer = invitedPlayer,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null,
                Accepted = false
            };

            _state.State.Invites[inviteId.ToString()] = invite;
            await _state.WriteStateAsync();

            return invite.InviteId;
        }

        public Task<WorldInvite?> GetInviteAsync(InviteId inviteId)
        {
            if (_state.State.Invites.TryGetValue(inviteId.Value, out var invite))
            {
                // Check if expired
                if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return Task.FromResult<WorldInvite?>(null);
                }

                return Task.FromResult<WorldInvite?>(invite);
            }

            return Task.FromResult<WorldInvite?>(null);
        }

        public async Task<bool> AcceptInviteAsync(InviteId inviteId)
        {
            if (!_state.State.Invites.TryGetValue(inviteId.Value, out var invite))
            {
                return false;
            }

            // Check if expired
            if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
            {
                return false;
            }

            // Check if already accepted
            if (invite.Accepted)
            {
                return false;
            }

            // Mark as accepted
            invite.Accepted = true;
            _state.State.Invites[inviteId.Value] = invite;
            await _state.WriteStateAsync();

            // Add player to ACL
            var worldId = new WorldId(this.GetPrimaryKeyString());
            var aclGrain = this.GrainFactory.GetGrain<IWorldAclGrain>(worldId.Value);
            await aclGrain.AddPlayerAsync(invite.InvitedPlayer);

            return true;
        }

        private void PurgeDeadInvites()
        {
            var now = DateTime.UtcNow;
            var deadKeys = _state.State.Invites
                .Where(kvp =>
                    (kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value < now) ||
                    (kvp.Value.Accepted && kvp.Value.CreatedAt < now - AcceptedInviteRetention))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in deadKeys)
                _state.State.Invites.Remove(key);
        }

        public Task<IReadOnlyList<WorldInvite>> GetPendingInvitesAsync(PlayerId playerId)
        {
            var now = DateTime.UtcNow;
            var pending = _state.State.Invites.Values
                .Where(inv => 
                    inv.InvitedPlayer.Equals(playerId) &&
                    !inv.Accepted &&
                    (!inv.ExpiresAt.HasValue || inv.ExpiresAt.Value >= now)
                )
                .ToList();

            return Task.FromResult<IReadOnlyList<WorldInvite>>(pending);
        }
    }

    /// <summary>
    /// State for the world invite grain.
    /// </summary>
    [GenerateSerializer]
    public class WorldInviteState
    {
        [Id(0)] public Dictionary<string, WorldInvite> Invites { get; set; } = new Dictionary<string, WorldInvite>();
    }
}

