using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model.Events;
using Aetherium.Model.Worlds;
using Aetherium.Server.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain for scheduling and managing procedural events within a world.
    /// Coordinates event scheduling, triggering, and instance management.
    /// </summary>
    public class EventSchedulerGrain : Grain, IEventSchedulerGrain
    {
        private readonly IPersistentState<EventSchedulerState> _state;
        private readonly IGrainFactory _grainFactory;
        private readonly Dictionary<string, IEventHandler> _handlers = new();

        public EventSchedulerGrain(
            [PersistentState("scheduler", "worldStore")] IPersistentState<EventSchedulerState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new EventSchedulerState
                {
                    WorldId = this.GetPrimaryKeyString(),
                    ScheduledEvents = new Dictionary<string, ScheduledEvent>(),
                    ActiveEventInstances = new List<string>(),
                    Handlers = new Dictionary<string, string>()
                };
            }

            // Register default handlers
            RegisterDefaultHandlers();

            return base.OnActivateAsync(cancellationToken);
        }

        private void RegisterDefaultHandlers()
        {
            // Register default handler types (will instantiate when needed)
            _state.State.Handlers["merchant_caravan"] = "MerchantCaravanHandler";
            _state.State.Handlers["monster_invasion"] = "MonsterInvasionHandler";

            // Create handler instances
            _handlers["merchant_caravan"] = new MerchantCaravanHandler();
            _handlers["monster_invasion"] = new MonsterInvasionHandler(
                this.ServiceProvider.GetService<SpawnManager>());
        }

        public async Task<string> ScheduleEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double scheduledGameTime,
            string? regionId = null)
        {
            var eventId = Guid.NewGuid().ToString();
            var scheduledEvent = new ScheduledEvent
            {
                EventId = eventId,
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = scheduledGameTime,
                RegionId = regionId,
                IsTriggered = false
            };

            _state.State.ScheduledEvents[eventId] = scheduledEvent;
            await _state.WriteStateAsync();
            return eventId;
        }

        public async Task<string> ScheduleEventAtLocationAsync(
            string eventType,
            Dictionary<string, object> eventData,
            int x, int y, int z)
        {
            var eventId = Guid.NewGuid().ToString();
            var scheduledEvent = new ScheduledEvent
            {
                EventId = eventId,
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = 0.0, // Trigger immediately if time not set
                X = x,
                Y = y,
                Z = z,
                IsTriggered = false
            };

            _state.State.ScheduledEvents[eventId] = scheduledEvent;
            await _state.WriteStateAsync();
            return eventId;
        }

        public async Task<string> ScheduleRecurringEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double intervalHours)
        {
            var eventId = Guid.NewGuid().ToString();
            var scheduledEvent = new ScheduledEvent
            {
                EventId = eventId,
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = 0.0, // Will be set on first trigger
                IsRecurring = true,
                RecurIntervalHours = intervalHours,
                IsTriggered = false
            };

            _state.State.ScheduledEvents[eventId] = scheduledEvent;
            await _state.WriteStateAsync();
            return eventId;
        }

        public async Task ProcessScheduledEventsAsync(double currentGameTime, int day)
        {
            var eventsToProcess = _state.State.ScheduledEvents.Values
                .Where(e => !e.IsTriggered && (e.ScheduledGameTime <= currentGameTime || e.ScheduledGameTime == 0.0))
                .ToList();

            foreach (var scheduledEvent in eventsToProcess)
            {
                await TriggerEventAsync(scheduledEvent.EventId, currentGameTime, day);
            }

            await _state.WriteStateAsync();
        }

        public async Task<bool> TriggerEventAsync(string eventId)
        {
            if (!_state.State.ScheduledEvents.TryGetValue(eventId, out var scheduledEvent))
                return false;

            // Get clock to determine current game time
            var clock = this.ServiceProvider.GetService<WorldClock>();
            if (clock == null)
                return false;

            var currentGameTime = clock.GetTotalGameTimeHours();
            var day = clock.GetDay();

            return await TriggerEventAsync(eventId, currentGameTime, day);
        }

        private async Task<bool> TriggerEventAsync(string eventId, double currentGameTime, int day)
        {
            if (!_state.State.ScheduledEvents.TryGetValue(eventId, out var scheduledEvent))
                return false;

            if (scheduledEvent.IsTriggered && !scheduledEvent.IsRecurring)
                return false;

            // Create event instance
            var eventInstanceId = new EventInstanceId(Guid.NewGuid().ToString());
            var instanceGrain = _grainFactory.GetGrain<IEventInstanceGrain>(eventInstanceId.Value);

            var worldId = new WorldId(this.GetPrimaryKeyString());
            var config = new EventInstanceConfig
            {
                EventInstanceId = eventInstanceId,
                EventId = scheduledEvent.EventId,
                EventType = scheduledEvent.EventType,
                WorldId = worldId,
                MapId = null, // Will be resolved from region if needed
                RegionId = scheduledEvent.RegionId,
                X = scheduledEvent.X,
                Y = scheduledEvent.Y,
                Z = scheduledEvent.Z,
                AreaOfInterestRadius = scheduledEvent.EventData.TryGetValue("aoiRadius", out var aoiObj) 
                    ? Convert.ToInt32(aoiObj) 
                    : 50,
                EventData = scheduledEvent.EventData,
                CreatedAt = DateTime.UtcNow,
                ScheduledGameTime = scheduledEvent.ScheduledGameTime
            };

            await instanceGrain.InitializeAsync(config);
            await instanceGrain.StartAsync(currentGameTime);

            // Track active instance
            _state.State.ActiveEventInstances.Add(eventInstanceId.Value);

            // Execute handler if available
            if (_handlers.TryGetValue(scheduledEvent.EventType, out var handler))
            {
                await handler.HandleEventAsync(scheduledEvent, currentGameTime, day);
            }

            // Update scheduled event state
            if (scheduledEvent.IsRecurring && scheduledEvent.RecurIntervalHours.HasValue)
            {
                scheduledEvent.ScheduledGameTime = currentGameTime + scheduledEvent.RecurIntervalHours.Value;
                scheduledEvent.IsTriggered = false;
            }
            else
            {
                scheduledEvent.IsTriggered = true;
            }

            await _state.WriteStateAsync();
            return true;
        }

        public async Task<bool> CancelEventAsync(string eventId)
        {
            if (_state.State.ScheduledEvents.TryGetValue(eventId, out var scheduledEvent))
            {
                _state.State.ScheduledEvents.Remove(eventId);
                await _state.WriteStateAsync();
                return true;
            }
            return false;
        }

        public Task<List<ScheduledEvent>> GetScheduledEventsAsync()
        {
            var events = _state.State.ScheduledEvents.Values
                .Where(e => !e.IsTriggered)
                .ToList();
            return Task.FromResult(events);
        }

        public Task<List<string>> GetActiveEventInstancesAsync()
        {
            return Task.FromResult(new List<string>(_state.State.ActiveEventInstances));
        }

        public async Task RegisterHandlerAsync(string eventType, string handlerType)
        {
            _state.State.Handlers[eventType] = handlerType;

            // Instantiate handler based on type
            IEventHandler? handler = handlerType switch
            {
                "MerchantCaravanHandler" => new MerchantCaravanHandler(),
                "MonsterInvasionHandler" => new MonsterInvasionHandler(
                    this.ServiceProvider.GetService<SpawnManager>()),
                _ => null
            };

            if (handler != null)
            {
                _handlers[eventType] = handler;
            }

            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// State for the event scheduler grain.
    /// </summary>
    [GenerateSerializer]
    public class EventSchedulerState
    {
        [Id(0)] public string WorldId { get; set; } = string.Empty;
        [Id(1)] public Dictionary<string, ScheduledEvent> ScheduledEvents { get; set; } = new Dictionary<string, ScheduledEvent>();
        [Id(2)] public List<string> ActiveEventInstances { get; set; } = new List<string>(); // EventInstanceId values
        [Id(3)] public Dictionary<string, string> Handlers { get; set; } = new Dictionary<string, string>(); // EventType -> HandlerType
    }
}

