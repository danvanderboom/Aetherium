using Orleans;
using Orleans.Runtime;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain managing access control for a world.
    /// </summary>
    public class WorldAclGrain : Grain, IWorldAclGrain
    {
        private readonly IPersistentState<WorldAclState> _state;

        public WorldAclGrain(
            [PersistentState("acl", "worldStore")] IPersistentState<WorldAclState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new WorldAclState
                {
                    Acl = new WorldAcl
                    {
                        AccessLevel = WorldAccessLevel.Public,
                        AllowedPlayers = new System.Collections.Generic.HashSet<PlayerId>(),
                        OwnerPlayers = new System.Collections.Generic.HashSet<PlayerId>()
                    }
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task<WorldAcl> GetAclAsync()
        {
            return Task.FromResult(_state.State.Acl);
        }

        public async Task SetAclAsync(WorldAcl acl)
        {
            _state.State.Acl = acl;
            await _state.WriteStateAsync();
        }

        public Task<bool> CanAccessAsync(PlayerId playerId)
        {
            var acl = _state.State.Acl;

            // Public worlds are accessible to all
            if (acl.AccessLevel == WorldAccessLevel.Public)
            {
                return Task.FromResult(true);
            }

            // Private worlds require explicit access
            return Task.FromResult(
                acl.OwnerPlayers.Contains(playerId) || 
                acl.AllowedPlayers.Contains(playerId)
            );
        }

        public async Task AddPlayerAsync(PlayerId playerId)
        {
            _state.State.Acl.AllowedPlayers.Add(playerId);
            await _state.WriteStateAsync();
        }

        public async Task RemovePlayerAsync(PlayerId playerId)
        {
            _state.State.Acl.AllowedPlayers.Remove(playerId);
            // Don't remove from OwnerPlayers - ownership is permanent
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// State for the world ACL grain.
    /// </summary>
    [GenerateSerializer]
    public class WorldAclState
    {
        [Id(0)] public WorldAcl Acl { get; set; } = new WorldAcl();
    }
}

