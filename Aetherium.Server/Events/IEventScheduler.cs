using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Interface for scheduling and managing procedural events.
    /// </summary>
    public interface IEventScheduler
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
        /// Cancels a scheduled event.
        /// </summary>
        Task<bool> CancelEventAsync(string eventId);

        /// <summary>
        /// Gets all scheduled events.
        /// </summary>
        Task<List<ScheduledEvent>> GetScheduledEventsAsync();
    }

    /// <summary>
    /// Represents a scheduled procedural event.
    /// </summary>
    public class ScheduledEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
        public double ScheduledGameTime { get; set; }
        public string? RegionId { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Z { get; set; }
        public bool IsRecurring { get; set; }
        public double? RecurIntervalHours { get; set; }
        public bool IsTriggered { get; set; }
    }
}

