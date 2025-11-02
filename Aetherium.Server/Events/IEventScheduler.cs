using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

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
    [GenerateSerializer]
    public class ScheduledEvent
    {
        [Id(0)] public string EventId { get; set; } = Guid.NewGuid().ToString();
        [Id(1)] public string EventType { get; set; } = string.Empty;
        [Id(2)] public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
        [Id(3)] public double ScheduledGameTime { get; set; }
        [Id(4)] public string? RegionId { get; set; }
        [Id(5)] public int? X { get; set; }
        [Id(6)] public int? Y { get; set; }
        [Id(7)] public int? Z { get; set; }
        [Id(8)] public bool IsRecurring { get; set; }
        [Id(9)] public double? RecurIntervalHours { get; set; }
        [Id(10)] public bool IsTriggered { get; set; }
    }
}

