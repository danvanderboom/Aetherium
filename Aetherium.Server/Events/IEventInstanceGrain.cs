using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model.Worlds;
using Aetherium.Model.Events;

namespace Aetherium.Server.Events
{
    /// <summary>
    /// Orleans grain interface for managing a single event instance.
    /// Keyed by event instance ID.
    /// </summary>
    public interface IEventInstanceGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Initializes the event instance with configuration.
        /// </summary>
        Task InitializeAsync(EventInstanceConfig config);

        /// <summary>
        /// Gets event instance information.
        /// </summary>
        Task<EventInstanceInfo?> GetInfoAsync();

        /// <summary>
        /// Starts the event instance.
        /// </summary>
        Task StartAsync(double currentGameTime);

        /// <summary>
        /// Updates the event instance (called during ticks).
        /// </summary>
        Task UpdateAsync(double currentGameTime, TimeSpan gameTimeElapsed);

        /// <summary>
        /// Completes the event instance.
        /// </summary>
        Task CompleteAsync();

        /// <summary>
        /// Cancels the event instance.
        /// </summary>
        Task CancelAsync();

        /// <summary>
        /// Gets the current state of the event instance.
        /// </summary>
        Task<EventInstanceState> GetStateAsync();

        /// <summary>
        /// Gets players in the event's area of interest.
        /// </summary>
        Task<List<PlayerId>> GetPlayersInAreaAsync();

        /// <summary>
        /// Broadcasts an event to players in the area of interest.
        /// </summary>
        Task BroadcastToAreaAsync(string eventType, Dictionary<string, object> eventData);
    }
}

