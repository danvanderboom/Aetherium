using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Manages game time with configurable tick rate and day length.
    /// Provides time-of-day calculations and conversion between real time and game time.
    /// </summary>
    /// <remarks>
    /// Registered as a process-wide singleton and driven concurrently by many region/world
    /// tick activations, so all mutable-state access is guarded by <see cref="_lock"/>.
    /// </remarks>
    public class WorldClock
    {
        private readonly SimulationOptions _options;
        private readonly object _lock = new();
        private DateTime _worldStartTime;
        private double _accumulatedGameTime;      // Game time in hours (authoritative)
        private double _accumulatedAtLastTick;    // Snapshot at the previous Tick(), for delta computation
        private DateTime _lastRealTime;           // Wall-clock anchor for lazy accumulation

        public WorldClock(IOptions<SimulationOptions> options)
        {
            _options = options.Value;
            var now = DateTime.UtcNow;
            _worldStartTime = now;
            _lastRealTime = now;
            _accumulatedGameTime = 0.0;
            _accumulatedAtLastTick = 0.0;
        }

        /// <summary>
        /// Gets the current game time in hours (0-24).
        /// </summary>
        public double GetTimeOfDay()
        {
            lock (_lock)
            {
                AdvanceLocked();
                return _accumulatedGameTime % 24.0;
            }
        }

        /// <summary>
        /// Gets the current game day (starts at 0).
        /// </summary>
        public int GetDay()
        {
            lock (_lock)
            {
                AdvanceLocked();
                return (int)(_accumulatedGameTime / 24.0);
            }
        }

        /// <summary>
        /// Gets the total game time in hours since world start.
        /// </summary>
        public double GetTotalGameTimeHours()
        {
            lock (_lock)
            {
                AdvanceLocked();
                return _accumulatedGameTime;
            }
        }

        /// <summary>
        /// Advances the clock by one tick (simulation step).
        /// Returns the elapsed game time since the previous <see cref="Tick"/> call — independent
        /// of any interleaved reads that also advance accumulated time.
        /// </summary>
        public TimeSpan Tick()
        {
            lock (_lock)
            {
                AdvanceLocked();
                var gameTimeElapsedHours = _accumulatedGameTime - _accumulatedAtLastTick;
                _accumulatedAtLastTick = _accumulatedGameTime;
                return TimeSpan.FromHours(gameTimeElapsedHours);
            }
        }

        /// <summary>
        /// Gets the time until the next hour (in game time).
        /// </summary>
        public TimeSpan GetTimeUntilNextHour()
        {
            double timeOfDay;
            lock (_lock)
            {
                AdvanceLocked();
                timeOfDay = _accumulatedGameTime % 24.0;
            }

            var hoursUntilNext = 1.0 - (timeOfDay % 1.0);
            var gameTimeUntilNext = TimeSpan.FromHours(hoursUntilNext);

            // Convert game time to real time
            var realTimeUntilNext = TimeSpan.FromSeconds(
                gameTimeUntilNext.TotalHours * (_options.DayLengthMinutes * 60.0) / 24.0
            );

            return realTimeUntilNext;
        }

        /// <summary>
        /// Sets the world start time (for loading saved worlds).
        /// </summary>
        public void SetWorldTime(double gameTimeHours)
        {
            lock (_lock)
            {
                _accumulatedGameTime = gameTimeHours;
                _accumulatedAtLastTick = gameTimeHours;
                _worldStartTime = DateTime.UtcNow;
                _lastRealTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Advances <see cref="_accumulatedGameTime"/> by the game time corresponding to the real
        /// time elapsed since the last update. Must be called while holding <see cref="_lock"/>.
        /// </summary>
        private void AdvanceLocked()
        {
            var now = DateTime.UtcNow;
            if (now > _lastRealTime)
            {
                var realTimeElapsed = now - _lastRealTime;
                var gameTimeElapsedHours = (realTimeElapsed.TotalSeconds / (_options.DayLengthMinutes * 60.0)) * 24.0;
                _accumulatedGameTime += gameTimeElapsedHours;
                _lastRealTime = now;
            }
        }

        /// <summary>
        /// Converts real time duration to game time duration. (Stateless — no lock required.)
        /// </summary>
        public TimeSpan RealTimeToGameTime(TimeSpan realTime)
        {
            var gameTimeHours = (realTime.TotalSeconds / (_options.DayLengthMinutes * 60.0)) * 24.0;
            return TimeSpan.FromHours(gameTimeHours);
        }

        /// <summary>
        /// Converts game time duration to real time duration. (Stateless — no lock required.)
        /// </summary>
        public TimeSpan GameTimeToRealTime(TimeSpan gameTime)
        {
            var realTimeSeconds = gameTime.TotalHours * (_options.DayLengthMinutes * 60.0) / 24.0;
            return TimeSpan.FromSeconds(realTimeSeconds);
        }
    }
}
