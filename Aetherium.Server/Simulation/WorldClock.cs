using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Aetherium.Server.Simulation
{
    /// <summary>
    /// Manages game time with configurable tick rate and day length.
    /// Provides time-of-day calculations and conversion between real time and game time.
    /// </summary>
    public class WorldClock
    {
        private readonly SimulationOptions _options;
        private DateTime _worldStartTime;
        private double _accumulatedGameTime; // Game time in hours
        private DateTime _lastTick;

        public WorldClock(IOptions<SimulationOptions> options)
        {
            _options = options.Value;
            _worldStartTime = DateTime.UtcNow;
            _lastTick = DateTime.UtcNow;
            _accumulatedGameTime = 0.0;
        }

        /// <summary>
        /// Gets the current game time in hours (0-24).
        /// </summary>
        public double GetTimeOfDay()
        {
            UpdateAccumulatedTime();
            return _accumulatedGameTime % 24.0;
        }

        /// <summary>
        /// Gets the current game day (starts at 0).
        /// </summary>
        public int GetDay()
        {
            UpdateAccumulatedTime();
            return (int)(_accumulatedGameTime / 24.0);
        }

        /// <summary>
        /// Gets the total game time in hours since world start.
        /// </summary>
        public double GetTotalGameTimeHours()
        {
            UpdateAccumulatedTime();
            return _accumulatedGameTime;
        }

        /// <summary>
        /// Advances the clock by one tick (simulation step).
        /// Returns the elapsed game time for this tick.
        /// </summary>
        public TimeSpan Tick()
        {
            var now = DateTime.UtcNow;
            var realTimeElapsed = now - _lastTick;
            _lastTick = now;

            // Convert real time to game time
            // Real time elapsed in seconds / (day length in minutes * 60) = fraction of day
            // Multiply by 24 hours to get game hours elapsed
            var gameTimeElapsedHours = (realTimeElapsed.TotalSeconds / (_options.DayLengthMinutes * 60.0)) * 24.0;
            _accumulatedGameTime += gameTimeElapsedHours;

            // Return elapsed game time for this tick
            return TimeSpan.FromHours(gameTimeElapsedHours);
        }

        /// <summary>
        /// Gets the time until the next hour (in game time).
        /// </summary>
        public TimeSpan GetTimeUntilNextHour()
        {
            var timeOfDay = GetTimeOfDay();
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
            _accumulatedGameTime = gameTimeHours;
            _worldStartTime = DateTime.UtcNow;
            _lastTick = DateTime.UtcNow;
        }

        private void UpdateAccumulatedTime()
        {
            var now = DateTime.UtcNow;
            if (now > _lastTick)
            {
                var realTimeElapsed = now - _lastTick;
                var gameTimeElapsedHours = (realTimeElapsed.TotalSeconds / (_options.DayLengthMinutes * 60.0)) * 24.0;
                _accumulatedGameTime += gameTimeElapsedHours;
                _lastTick = now;
            }
        }

        /// <summary>
        /// Converts real time duration to game time duration.
        /// </summary>
        public TimeSpan RealTimeToGameTime(TimeSpan realTime)
        {
            var gameTimeHours = (realTime.TotalSeconds / (_options.DayLengthMinutes * 60.0)) * 24.0;
            return TimeSpan.FromHours(gameTimeHours);
        }

        /// <summary>
        /// Converts game time duration to real time duration.
        /// </summary>
        public TimeSpan GameTimeToRealTime(TimeSpan gameTime)
        {
            var realTimeSeconds = gameTime.TotalHours * (_options.DayLengthMinutes * 60.0) / 24.0;
            return TimeSpan.FromSeconds(realTimeSeconds);
        }
    }
}

