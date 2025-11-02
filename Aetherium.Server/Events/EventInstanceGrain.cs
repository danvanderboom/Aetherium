using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Events;
using Aetherium.Model.Worlds;
using Aetherium.Server.MultiWorld;
using EventInstanceState = Aetherium.Model.Events.EventInstanceState;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain managing a single event instance.
    /// Handles event lifecycle, AOI tracking, and broadcasts.
    /// </summary>
    public class EventInstanceGrain : Grain, IEventInstanceGrain
    {
        private readonly IPersistentState<EventInstanceGrainState> _state;
        private readonly IGrainFactory _grainFactory;

        public EventInstanceGrain(
            [PersistentState("eventInstance", "worldStore")] IPersistentState<EventInstanceGrainState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new EventInstanceGrainState
                {
                    EventInstanceId = new EventInstanceId(this.GetPrimaryKeyString()),
                    State = EventInstanceState.Scheduled,
                    PlayersInArea = new List<PlayerId>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public async Task InitializeAsync(EventInstanceConfig config)
        {
            if (_state.State == null)
                throw new InvalidOperationException("State not initialized");

            // Resolve MapId from RegionId if provided and MapId is not set
            if (!string.IsNullOrEmpty(config.RegionId) && string.IsNullOrEmpty(config.MapId))
            {
                var regionGrain = _grainFactory.GetGrain<IMapRegionGrain>(config.RegionId);
                var snapshot = await regionGrain.GetSnapshotAsync();
                config.MapId = snapshot.MapId;
            }

            _state.State.EventInstanceId = config.EventInstanceId;
            _state.State.EventId = config.EventId;
            _state.State.EventType = config.EventType;
            _state.State.WorldId = config.WorldId;
            _state.State.MapId = config.MapId;
            _state.State.RegionId = config.RegionId;
            _state.State.X = config.X;
            _state.State.Y = config.Y;
            _state.State.Z = config.Z;
            _state.State.AreaOfInterestRadius = config.AreaOfInterestRadius;
            _state.State.EventData = new Dictionary<string, object>(config.EventData);
            _state.State.CreatedAt = config.CreatedAt;
            _state.State.ScheduledGameTime = config.ScheduledGameTime;
            _state.State.State = EventInstanceState.Scheduled;

            await _state.WriteStateAsync();
        }

        public Task<EventInstanceInfo?> GetInfoAsync()
        {
            if (_state.State == null)
                return Task.FromResult<EventInstanceInfo?>(null);

            var info = new EventInstanceInfo
            {
                EventInstanceId = _state.State.EventInstanceId,
                EventId = _state.State.EventId,
                EventType = _state.State.EventType,
                WorldId = _state.State.WorldId,
                MapId = _state.State.MapId,
                RegionId = _state.State.RegionId,
                X = _state.State.X,
                Y = _state.State.Y,
                Z = _state.State.Z,
                AreaOfInterestRadius = _state.State.AreaOfInterestRadius,
                State = _state.State.State,
                CreatedAt = _state.State.CreatedAt,
                StartedAt = _state.State.StartedAt,
                CompletedAt = _state.State.CompletedAt,
                ScheduledGameTime = _state.State.ScheduledGameTime,
                PlayersInArea = new List<PlayerId>(_state.State.PlayersInArea)
            };

            return Task.FromResult<EventInstanceInfo?>(info);
        }

        public async Task StartAsync(double currentGameTime)
        {
            if (_state.State == null)
                return;

            _state.State.State = EventInstanceState.Starting;
            _state.State.StartedAt = DateTime.UtcNow;

            // Update players in area
            await UpdatePlayersInAreaAsync();

            _state.State.State = EventInstanceState.Active;
            await _state.WriteStateAsync();
        }

        public async Task UpdateAsync(double currentGameTime, TimeSpan gameTimeElapsed)
        {
            if (_state.State == null || _state.State.State != EventInstanceState.Active)
                return;

            // Update players in area periodically
            await UpdatePlayersInAreaAsync();

            // Broadcast updates to players in area
            // This would typically include entity spawns, state changes, etc.
            // For now, just update the state
            await _state.WriteStateAsync();
        }

        public async Task CompleteAsync()
        {
            if (_state.State == null)
                return;

            _state.State.State = EventInstanceState.Completing;
            _state.State.CompletedAt = DateTime.UtcNow;
            _state.State.State = EventInstanceState.Completed;

            await _state.WriteStateAsync();

            // Despawn any entities spawned for this event via the spawn controller
            try
            {
                var spawnController = _grainFactory.GetGrain<ISpawnControllerGrain>(_state.State.EventInstanceId.Value);
                var spawnedEntities = await spawnController.GetSpawnedEntitiesAsync();
                if (spawnedEntities.Count > 0)
                {
                    await spawnController.DespawnEntitiesAsync(spawnedEntities);
                }
            }
            catch
            {
                // Swallow errors for cleanup path; scheduler cleanup still proceeds
            }

            // Notify scheduler to remove from active instances
            var schedulerGrain = _grainFactory.GetGrain<IEventSchedulerGrain>(_state.State.WorldId.Value);
            // Scheduler will clean up on next tick

            DeactivateOnIdle();
        }

        public async Task CancelAsync()
        {
            if (_state.State == null)
                return;

            _state.State.State = EventInstanceState.Cancelled;
            await _state.WriteStateAsync();

            DeactivateOnIdle();
        }

        public Task<EventInstanceState> GetStateAsync()
        {
            if (_state.State == null)
                return Task.FromResult(EventInstanceState.Scheduled);

            return Task.FromResult(_state.State.State);
        }

        public Task<List<PlayerId>> GetPlayersInAreaAsync()
        {
            if (_state.State == null)
                return Task.FromResult(new List<PlayerId>());

            return Task.FromResult(new List<PlayerId>(_state.State.PlayersInArea));
        }

        public async Task BroadcastToAreaAsync(string eventType, Dictionary<string, object> eventData)
        {
            if (_state.State == null)
                return;

            // Get players in area
            var playersInArea = await GetPlayersInAreaAsync();

            // Broadcast to each player (via their session/hub connection)
            // This would typically go through SignalR hub or game session manager
            // For now, we'll track it in the event data
            eventData["broadcastTo"] = playersInArea.Select(p => p.Value).ToList();

            await _state.WriteStateAsync();
        }

        private async Task UpdatePlayersInAreaAsync()
        {
            if (_state.State == null || !_state.State.X.HasValue || !_state.State.Y.HasValue || !_state.State.Z.HasValue)
                return;

            // Get world grain to find players near event location
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(_state.State.WorldId.Value);
            
            // If we have a map ID, query players on that map
            // Otherwise, query all maps in the world
            List<string> mapIds;
            if (!string.IsNullOrEmpty(_state.State.MapId))
            {
                mapIds = new List<string> { _state.State.MapId };
            }
            else
            {
                mapIds = await worldGrain.GetMapIdsAsync();
            }

            var playersInArea = new List<PlayerId>();
            var eventX = _state.State.X.Value;
            var eventY = _state.State.Y.Value;
            var eventZ = _state.State.Z.Value;
            var radius = _state.State.AreaOfInterestRadius;

            // Query each map for players within AOI
            foreach (var mapId in mapIds)
            {
                var mapGrain = _grainFactory.GetGrain<IGameMapGrain>(mapId);
                var players = await mapGrain.GetPlayersAsync();

                // TODO: Get player locations and filter by distance
                // For now, just track all players on the map if location matches
                // In a full implementation, we'd query player positions and calculate distance
                foreach (var playerId in players)
                {
                    // Simple check: if event is in a region, check if player is on same map
                    // Full AOI check would require player location queries
                    playersInArea.Add(new PlayerId(playerId));
                }
            }

            _state.State.PlayersInArea = playersInArea;
        }
    }

    /// <summary>
    /// Persisted state for an event instance grain.
    /// </summary>
    [GenerateSerializer]
    public class EventInstanceGrainState
    {
        [Id(0)] public EventInstanceId EventInstanceId { get; set; }
        [Id(1)] public string EventId { get; set; } = string.Empty;
        [Id(2)] public string EventType { get; set; } = string.Empty;
        [Id(3)] public WorldId WorldId { get; set; }
        [Id(4)] public string? MapId { get; set; }
        [Id(5)] public string? RegionId { get; set; }
        [Id(6)] public int? X { get; set; }
        [Id(7)] public int? Y { get; set; }
        [Id(8)] public int? Z { get; set; }
        [Id(9)] public int AreaOfInterestRadius { get; set; } = 50;
        [Id(10)] public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
        [Id(11)] public DateTime CreatedAt { get; set; }
        [Id(12)] public DateTime? StartedAt { get; set; }
        [Id(13)] public DateTime? CompletedAt { get; set; }
        [Id(14)] public double ScheduledGameTime { get; set; }
        [Id(15)] public EventInstanceState State { get; set; }
        [Id(16)] public List<PlayerId> PlayersInArea { get; set; } = new List<PlayerId>();
    }
}

