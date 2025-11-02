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
    /// Orleans grain managing the world directory.
    /// </summary>
    public class WorldDirectoryGrain : Grain, IWorldDirectoryGrain
    {
        private readonly IPersistentState<WorldDirectoryState> _state;

        public WorldDirectoryGrain(
            [PersistentState("directory", "worldStore")] IPersistentState<WorldDirectoryState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new WorldDirectoryState
                {
                    Worlds = new Dictionary<string, WorldSummary>(),
                    DefaultWorldId = null
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task RegisterWorldAsync(WorldId worldId, WorldSummary summary)
        {
            _state.State.Worlds[worldId.Value] = summary;
            return _state.WriteStateAsync();
        }

        public Task UnregisterWorldAsync(WorldId worldId)
        {
            _state.State.Worlds.Remove(worldId.Value);
            return _state.WriteStateAsync();
        }

        public Task<IReadOnlyList<WorldSummary>> ListWorldsAsync(WorldQuery query)
        {
            var results = _state.State.Worlds.Values.AsEnumerable();

            // Filter by access level
            if (query.AccessLevel.HasValue)
            {
                results = results.Where(w => w.AccessLevel == query.AccessLevel.Value);
            }

            // Filter by player access (for private worlds)
            if (query.PlayerId.HasValue)
            {
                // This will be enhanced with ACL checks from WorldAclGrain
                // For now, show public worlds and any world the player owns/is invited to
                var playerId = query.PlayerId.Value.Value;
                results = results.Where(w => 
                    w.AccessLevel == WorldAccessLevel.Public 
                    // TODO: Check ACL from WorldAclGrain for private worlds
                );
            }

            // Limit results
            if (query.MaxResults.HasValue)
            {
                results = results.Take(query.MaxResults.Value);
            }

            return Task.FromResult<IReadOnlyList<WorldSummary>>(results.OrderByDescending(w => w.LastActivityAt ?? w.CreatedAt).ToList());
        }

        public Task<WorldId?> GetDefaultWorldAsync()
        {
            if (_state.State.DefaultWorldId != null)
            {
                return Task.FromResult<WorldId?>(new WorldId(_state.State.DefaultWorldId));
            }
            return Task.FromResult<WorldId?>(null);
        }

        public Task SetDefaultWorldAsync(WorldId worldId)
        {
            _state.State.DefaultWorldId = worldId.Value;
            return _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// State for the world directory grain.
    /// </summary>
    [GenerateSerializer]
    public class WorldDirectoryState
    {
        [Id(0)] public Dictionary<string, WorldSummary> Worlds { get; set; } = new Dictionary<string, WorldSummary>();
        [Id(1)] public string? DefaultWorldId { get; set; }
    }
}

