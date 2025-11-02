using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Instances;
using Aetherium.Model.Worlds;
using Aetherium.Model.Groups;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Instances
{
    /// <summary>
    /// Orleans grain managing a single dungeon instance.
    /// Handles instance lifecycle, player management, and world state.
    /// </summary>
    public class DungeonInstanceGrain : Grain, IDungeonInstanceGrain
    {
        private readonly IPersistentState<DungeonInstanceState> _state;
        private readonly IGrainFactory _grainFactory;

        public DungeonInstanceGrain(
            [PersistentState("instance", "worldStore")] IPersistentState<DungeonInstanceState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new DungeonInstanceState
                {
                    InstanceId = new InstanceId(this.GetPrimaryKeyString()),
                    PlayerIds = new List<PlayerId>(),
                    State = InstanceState.Creating
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task InitializeAsync(InstanceConfig config)
        {
            if (_state.State == null)
                throw new InvalidOperationException("State not initialized");

            _state.State.InstanceId = config.InstanceId;
            _state.State.DungeonId = config.DungeonId;
            _state.State.WorldId = config.WorldId;
            _state.State.DungeonName = config.DungeonName;
            _state.State.State = InstanceState.Creating;
            _state.State.CreatedAt = config.CreatedAt;
            _state.State.PartyId = config.PartyId;
            _state.State.PlayerIds = new List<PlayerId>(config.PlayerIds);
            _state.State.MaxPlayers = config.GeneratorParameters.ContainsKey("MaxPlayers") 
                ? Convert.ToInt32(config.GeneratorParameters["MaxPlayers"]) 
                : 10;
            _state.State.LockoutKey = config.LockoutKey;

            // Map ID should be set by allocator when creating the map
            // For now, we'll store it in the config or retrieve it from world grain
            if (config.GeneratorParameters.ContainsKey("mapId"))
            {
                _state.State.MapId = config.GeneratorParameters["mapId"] as string;
            }

            _state.State.State = InstanceState.Active;
            _state.State.LastActivityAt = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }

        public Task<InstanceInfo?> GetInfoAsync()
        {
            if (_state.State == null || _state.State.State == InstanceState.Creating)
                return Task.FromResult<InstanceInfo?>(null);

            var info = new InstanceInfo
            {
                InstanceId = _state.State.InstanceId,
                DungeonId = _state.State.DungeonId,
                WorldId = _state.State.WorldId,
                DungeonName = _state.State.DungeonName,
                State = _state.State.State,
                PlayerCount = _state.State.PlayerIds.Count,
                MaxPlayers = _state.State.MaxPlayers,
                CreatedAt = _state.State.CreatedAt,
                LastActivityAt = _state.State.LastActivityAt,
                PartyId = _state.State.PartyId,
                PlayerIds = new List<PlayerId>(_state.State.PlayerIds),
                MapId = _state.State.MapId,
                LockoutKey = _state.State.LockoutKey
            };

            return Task.FromResult<InstanceInfo?>(info);
        }

        public async Task<bool> AddPlayersAsync(List<PlayerId> playerIds)
        {
            if (_state.State == null || _state.State.State != InstanceState.Active)
                return false;

            // Check capacity
            var currentCount = _state.State.PlayerIds.Count;
            if (currentCount + playerIds.Count > _state.State.MaxPlayers)
                return false;

            // Add new players
            foreach (var playerId in playerIds)
            {
                if (!_state.State.PlayerIds.Contains(playerId))
                {
                    _state.State.PlayerIds.Add(playerId);

                    // Move player to instance map if map is set
                    if (!string.IsNullOrEmpty(_state.State.MapId))
                    {
                        var worldGrain = _grainFactory.GetGrain<IWorldGrain>(_state.State.WorldId.Value);
                        await worldGrain.MovePlayerToMapAsync(playerId.Value, _state.State.MapId);
                    }
                }
            }

            _state.State.LastActivityAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            return true;
        }

        public async Task RemovePlayerAsync(PlayerId playerId)
        {
            if (_state.State == null)
                return;

            _state.State.PlayerIds.Remove(playerId);
            _state.State.LastActivityAt = DateTime.UtcNow;

            // If no players left, mark as abandoned (will be cleaned up later)
            if (_state.State.PlayerIds.Count == 0 && _state.State.State == InstanceState.Active)
            {
                _state.State.State = InstanceState.Abandoned;
            }

            await _state.WriteStateAsync();
        }

        public Task<List<PlayerId>> GetPlayersAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new List<PlayerId>());

            return Task.FromResult(new List<PlayerId>(_state.State.PlayerIds));
        }

        public Task<bool> IsPlayerInInstanceAsync(PlayerId playerId)
        {
            if (_state.State == null)
                return Task.FromResult(false);

            return Task.FromResult(_state.State.PlayerIds.Contains(playerId));
        }

        public async Task TickAsync(TimeSpan gameTimeElapsed)
        {
            if (_state.State == null || _state.State.State != InstanceState.Active)
                return;

            // If we have a map, tick it
            if (!string.IsNullOrEmpty(_state.State.MapId))
            {
                var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(_state.State.MapId);
                await mapGrain.TickAsync(gameTimeElapsed);
            }

            _state.State.LastActivityAt = DateTime.UtcNow;
            // Note: Not persisting on every tick to avoid excessive writes
        }

        public Task<string?> GetMapIdAsync()
        {
            if (_state.State == null)
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>(_state.State.MapId);
        }

        public async Task ShutdownAsync()
        {
            if (_state.State == null)
                return;

            _state.State.State = InstanceState.ShuttingDown;

            // Remove all players from instance
            foreach (var playerId in _state.State.PlayerIds.ToList())
            {
                await RemovePlayerAsync(playerId);
            }

            _state.State.State = InstanceState.Stopped;
            await _state.WriteStateAsync();

            // Notify allocator to release this instance
            var allocatorGrain = _grainFactory.GetGrain<IInstanceAllocatorGrain>(_state.State.WorldId.Value);
            await allocatorGrain.ReleaseInstanceAsync(_state.State.InstanceId);

            DeactivateOnIdle();
        }
    }

    /// <summary>
    /// Persisted state for a dungeon instance grain.
    /// </summary>
    [GenerateSerializer]
    public class DungeonInstanceState
    {
        [Id(0)] public InstanceId InstanceId { get; set; }
        [Id(1)] public DungeonId DungeonId { get; set; }
        [Id(2)] public WorldId WorldId { get; set; }
        [Id(3)] public string DungeonName { get; set; } = string.Empty;
        [Id(4)] public InstanceState State { get; set; }
        [Id(5)] public List<PlayerId> PlayerIds { get; set; } = new List<PlayerId>();
        [Id(6)] public int MaxPlayers { get; set; } = 10;
        [Id(7)] public DateTime CreatedAt { get; set; }
        [Id(8)] public DateTime? LastActivityAt { get; set; }
        [Id(9)] public PartyId? PartyId { get; set; }
        [Id(10)] public string? MapId { get; set; }
        [Id(11)] public LockoutKey? LockoutKey { get; set; }
    }
}

