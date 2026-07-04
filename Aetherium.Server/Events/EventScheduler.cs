using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Schedules and manages procedural events (merchant caravans, monster invasions, etc.).
    /// </summary>
    public class EventScheduler : IEventScheduler
    {
        private readonly SimulationOptions _options;
        private readonly Dictionary<string, ScheduledEvent> _events = new();
        private readonly Dictionary<string, IEventHandler> _handlers = new();

        // Registered as a process-wide singleton and scheduled/processed concurrently by many
        // region ticks, so all dictionary access is guarded by this lock. The lock is never held
        // across the handler await in ProcessScheduledEventsAsync (snapshot → await → update).
        private readonly object _lock = new();

        public EventScheduler(IOptions<SimulationOptions> options)
        {
            _options = options.Value;
            RegisterDefaultHandlers();
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler("merchant_caravan", new MerchantCaravanHandler());
            RegisterHandler("monster_invasion", new MonsterInvasionHandler());
        }

        public void RegisterHandler(string eventType, IEventHandler handler)
        {
            lock (_lock)
            {
                _handlers[eventType] = handler;
            }
        }

        public Task<string> ScheduleEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double scheduledGameTime,
            string? regionId = null)
        {
            if (!_options.EnableProceduralEvents)
                return Task.FromResult<string>(Guid.Empty.ToString());

            var scheduledEvent = new ScheduledEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = scheduledGameTime,
                RegionId = regionId,
                IsTriggered = false
            };

            lock (_lock)
            {
                _events[scheduledEvent.EventId] = scheduledEvent;
            }
            return Task.FromResult(scheduledEvent.EventId);
        }

        public Task<string> ScheduleEventAtLocationAsync(
            string eventType,
            Dictionary<string, object> eventData,
            int x, int y, int z)
        {
            if (!_options.EnableProceduralEvents)
                return Task.FromResult<string>(Guid.Empty.ToString());

            var scheduledEvent = new ScheduledEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = 0.0, // Trigger immediately if time not set
                X = x,
                Y = y,
                Z = z,
                IsTriggered = false
            };

            lock (_lock)
            {
                _events[scheduledEvent.EventId] = scheduledEvent;
            }
            return Task.FromResult(scheduledEvent.EventId);
        }

        public Task<string> ScheduleRecurringEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double intervalHours)
        {
            if (!_options.EnableProceduralEvents)
                return Task.FromResult<string>(Guid.Empty.ToString());

            var scheduledEvent = new ScheduledEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                EventData = eventData,
                ScheduledGameTime = 0.0, // Will be set on first trigger
                IsRecurring = true,
                RecurIntervalHours = intervalHours,
                IsTriggered = false
            };

            lock (_lock)
            {
                _events[scheduledEvent.EventId] = scheduledEvent;
            }
            return Task.FromResult(scheduledEvent.EventId);
        }

        public async Task ProcessScheduledEventsAsync(double currentGameTime, int day)
        {
            if (!_options.EnableProceduralEvents)
                return;

            // Snapshot the due events (and resolve their handlers) under the lock, then run the
            // handlers without holding it — handlers may re-enter the scheduler and awaiting under
            // a lock would deadlock or serialize the whole cluster's event processing.
            var due = new List<(ScheduledEvent Event, IEventHandler Handler)>();
            lock (_lock)
            {
                foreach (var e in _events.Values)
                {
                    if (e.IsTriggered || (e.ScheduledGameTime > currentGameTime && e.ScheduledGameTime != 0.0))
                        continue;
                    if (_handlers.TryGetValue(e.EventType, out var handler))
                        due.Add((e, handler));
                }
            }

            foreach (var (scheduledEvent, handler) in due)
            {
                await handler.HandleEventAsync(scheduledEvent, currentGameTime, day);

                lock (_lock)
                {
                    if (scheduledEvent.IsRecurring && scheduledEvent.RecurIntervalHours.HasValue)
                    {
                        // Reschedule for next occurrence
                        scheduledEvent.ScheduledGameTime = currentGameTime + scheduledEvent.RecurIntervalHours.Value;
                        scheduledEvent.IsTriggered = false;
                    }
                    else
                    {
                        scheduledEvent.IsTriggered = true;
                    }
                }
            }
        }

        public Task<bool> CancelEventAsync(string eventId)
        {
            lock (_lock)
            {
                return Task.FromResult(_events.Remove(eventId));
            }
        }

        public Task<List<ScheduledEvent>> GetScheduledEventsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_events.Values.Where(e => !e.IsTriggered).ToList());
            }
        }
    }

    /// <summary>
    /// Interface for event handlers.
    /// </summary>
    public interface IEventHandler
    {
        Task HandleEventAsync(ScheduledEvent scheduledEvent, double currentGameTime, int day);
    }
}

