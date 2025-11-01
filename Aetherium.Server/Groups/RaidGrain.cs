using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.Groups
{
    /// <summary>
    /// Orleans grain managing a raid (larger party, up to 40 members).
    /// </summary>
    public class RaidGrain : Grain, IRaidGrain
    {
        private readonly IPersistentState<RaidState> _state;

        public RaidGrain([PersistentState("raid", "worldStore")] IPersistentState<RaidState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new RaidState
                {
                    RaidId = new RaidId(this.GetPrimaryKeyString()),
                    Members = new List<PartyMember>(),
                    CreatedAt = DateTime.UtcNow
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task CreateAsync(PlayerId leaderId, string leaderName)
        {
            if (_state.State.Members.Count > 0)
            {
                throw new InvalidOperationException("Raid already exists");
            }

            _state.State.RaidId = new RaidId(this.GetPrimaryKeyString());
            _state.State.Members.Add(new PartyMember
            {
                PlayerId = leaderId,
                Name = leaderName,
                Role = PartyRole.Leader,
                JoinedAt = DateTime.UtcNow,
                IsOnline = true
            });
            _state.State.CreatedAt = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }

        public Task<RaidInfo?> GetInfoAsync()
        {
            if (_state.State.Members.Count == 0)
                return Task.FromResult<RaidInfo?>(null);

            var info = new RaidInfo
            {
                RaidId = _state.State.RaidId,
                Name = $"Raid of {_state.State.Members.Count}",
                Members = new List<PartyMember>(_state.State.Members),
                MaxMembers = 40,
                CreatedAt = _state.State.CreatedAt,
                WorldId = _state.State.WorldId
            };

            return Task.FromResult<RaidInfo?>(info);
        }

        public async Task<bool> AddMemberAsync(PlayerId playerId, string playerName)
        {
            if (_state.State.Members.Count >= 40)
                return false;

            if (_state.State.Members.Any(m => m.PlayerId.Equals(playerId)))
                return false; // Already in raid

            _state.State.Members.Add(new PartyMember
            {
                PlayerId = playerId,
                Name = playerName,
                Role = PartyRole.Member,
                JoinedAt = DateTime.UtcNow,
                IsOnline = true
            });

            await _state.WriteStateAsync();
            return true;
        }

        public async Task RemoveMemberAsync(PlayerId playerId)
        {
            var member = _state.State.Members.FirstOrDefault(m => m.PlayerId.Equals(playerId));
            if (member == null)
                return;

            _state.State.Members.Remove(member);

            // If leader left, assign new leader
            if (member.Role == PartyRole.Leader && _state.State.Members.Count > 0)
            {
                _state.State.Members[0].Role = PartyRole.Leader;
            }

            await _state.WriteStateAsync();

            // If raid is empty, deactivate grain
            if (_state.State.Members.Count == 0)
            {
                DeactivateOnIdle();
            }
        }

        public async Task<bool> SetLeaderAsync(PlayerId newLeaderId)
        {
            var newLeader = _state.State.Members.FirstOrDefault(m => m.PlayerId.Equals(newLeaderId));
            if (newLeader == null)
                return false;

            var currentLeader = _state.State.Members.FirstOrDefault(m => m.Role == PartyRole.Leader);
            if (currentLeader != null)
            {
                currentLeader.Role = PartyRole.Member;
            }

            newLeader.Role = PartyRole.Leader;
            await _state.WriteStateAsync();
            return true;
        }

        public Task<bool> IsMemberAsync(PlayerId playerId)
        {
            var isMember = _state.State.Members.Any(m => m.PlayerId.Equals(playerId));
            return Task.FromResult(isMember);
        }

        public Task<List<PlayerId>> GetMemberIdsAsync()
        {
            var ids = _state.State.Members.Select(m => m.PlayerId).ToList();
            return Task.FromResult(ids);
        }
    }

    /// <summary>
    /// State for the raid grain.
    /// </summary>
    [GenerateSerializer]
    public class RaidState
    {
        [Id(0)] public RaidId RaidId { get; set; }
        [Id(1)] public List<PartyMember> Members { get; set; } = new List<PartyMember>();
        [Id(2)] public DateTime CreatedAt { get; set; }
        [Id(3)] public WorldId? WorldId { get; set; }
    }
}

