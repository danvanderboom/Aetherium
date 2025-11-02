using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain interface for scheduling and managing procedural events within a world.
    /// Keyed by world ID.
    /// </summary>
    public interface IEventSchedulerGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Schedules an event to occur at a specific game time.
        /// </summary>
        Task<string> ScheduleEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double scheduledGameTime,
            string? regionId = null);

        /// <summary>
        /// Schedules an event to occur at a specific location.
        /// </summary>
        Task<string> ScheduleEventAtLocationAsync(
            string eventType,
            Dictionary<string, object> eventData,
            int x, int y, int z);

        /// <summary>
        /// Schedules a recurring event.
        /// </summary>
        Task<string> ScheduleRecurringEventAsync(
            string eventType,
            Dictionary<string, object> eventData,
            double intervalHours);

        /// <summary>
        /// Processes scheduled events for the current game time.
        /// </summary>
        Task ProcessScheduledEventsAsync(double currentGameTime, int day);

        /// <summary>
        /// Manually triggers an event by ID.
        /// </summary>
        Task<bool> TriggerEventAsync(string eventId);

        /// <summary>
        /// Cancels a scheduled event.
        /// </summary>
        Task<bool> CancelEventAsync(string eventId);

        /// <summary>
        /// Gets all scheduled events (not yet triggered).
        /// </summary>
        Task<List<ScheduledEvent>> GetScheduledEventsAsync();

        /// <summary>
        /// Gets all active event instances.
        /// </summary>
        Task<List<string>> GetActiveEventInstancesAsync();

        /// <summary>
        /// Registers an event handler for an event type.
        /// </summary>
        Task RegisterHandlerAsync(string eventType, string handlerType);
    }
}

