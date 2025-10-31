using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Interface for time-based world modifications.
    /// Implementations apply changes to regions based on elapsed game time.
    /// </summary>
    public interface ITemporalModifier
    {
        /// <summary>
        /// Gets the name/identifier of this modifier.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the priority of this modifier (lower = higher priority).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Applies time-based modifications to a region.
        /// </summary>
        /// <param name="region">The region grain to modify</param>
        /// <param name="regionSnapshot">Current region state snapshot</param>
        /// <param name="gameTimeElapsed">Elapsed game time for this tick</param>
        /// <param name="timeOfDay">Current time of day in hours (0-24)</param>
        /// <param name="day">Current day number</param>
        Task ApplyAsync(
            IMapRegionGrain region,
            Aetherium.Server.Persistence.RegionStateSnapshot regionSnapshot,
            TimeSpan gameTimeElapsed,
            double timeOfDay,
            int day);
    }

    /// <summary>
    /// Registry for temporal modifiers.
    /// </summary>
    public class TemporalModifierRegistry
    {
        private readonly List<ITemporalModifier> _modifiers = new();

        /// <summary>
        /// Registers a temporal modifier.
        /// </summary>
        public void Register(ITemporalModifier modifier)
        {
            _modifiers.Add(modifier);
            _modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// Gets all registered modifiers in priority order.
        /// </summary>
        public IReadOnlyList<ITemporalModifier> GetModifiers()
        {
            return _modifiers.AsReadOnly();
        }

        /// <summary>
        /// Applies all registered modifiers to a region.
        /// </summary>
        public async Task ApplyAllAsync(
            IMapRegionGrain region,
            Aetherium.Server.Persistence.RegionStateSnapshot regionSnapshot,
            TimeSpan gameTimeElapsed,
            double timeOfDay,
            int day)
        {
            foreach (var modifier in _modifiers)
            {
                await modifier.ApplyAsync(region, regionSnapshot, gameTimeElapsed, timeOfDay, day);
            }
        }
    }
}

