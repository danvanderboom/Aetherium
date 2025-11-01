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
    /// Orleans grain managing a party.
    /// </summary>
    public class PartyGrain : Grain, IPartyGrain
    {
        private readonly IPersistentState<PartyState> _state;

        public PartyGrain([PersistentState("party", "worldStore")] IPersistentState<PartyState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new PartyState
                {
                    PartyId = new PartyId(this.GetPrimaryKeyString()),
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
                throw new InvalidOperationException("Party already exists");
            }

            _state.State.PartyId = new PartyId(this.GetPrimaryKeyString());
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

        public Task<PartyInfo?> GetInfoAsync()
        {
            if (_state.State.Members.Count == 0)
                return Task.FromResult<PartyInfo?>(null);

            var info = new PartyInfo
            {
                PartyId = _state.State.PartyId,
                Name = $"Party of {_state.State.Members.Count}",
                Members = new List<PartyMember>(_state.State.Members),
                MaxMembers = 5,
                CreatedAt = _state.State.CreatedAt,
                WorldId = _state.State.WorldId
            };

            return Task.FromResult<PartyInfo?>(info);
        }

        public async Task<bool> AddMemberAsync(PlayerId playerId, string playerName)
        {
            if (_state.State.Members.Count >= 5)
                return false;

            if (_state.State.Members.Any(m => m.PlayerId.Equals(playerId)))
                return false; // Already in party

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

            // If party is empty, deactivate grain
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

        public async Task UpdateMemberStatusAsync(PlayerId playerId, bool isOnline)
        {
            var member = _state.State.Members.FirstOrDefault(m => m.PlayerId.Equals(playerId));
            if (member != null)
            {
                member.IsOnline = isOnline;
                await _state.WriteStateAsync();
            }
        }
    }

    /// <summary>
    /// State for the party grain.
    /// </summary>
    [GenerateSerializer]
    public class PartyState
    {
        [Id(0)] public PartyId PartyId { get; set; }
        [Id(1)] public List<PartyMember> Members { get; set; } = new List<PartyMember>();
        [Id(2)] public DateTime CreatedAt { get; set; }
        [Id(3)] public WorldId? WorldId { get; set; }
    }
}

