using System;
using System.Threading;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Simulation
{
    [TestFixture]
    public class WorldClockTests
    {
        [Test]
        public void WorldClock_Initializes_WithZeroTime()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 24
            });
            
            var clock = new WorldClock(options);
            
            Assert.AreEqual(0.0, clock.GetTimeOfDay(), 0.1);
            Assert.AreEqual(0, clock.GetDay());
        }

        [Test]
        public void WorldClock_Tick_AdvancesGameTime()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 1 // 1 minute = 1 day for faster testing
            });
            
            var clock = new WorldClock(options);
            
            // Wait a bit to ensure time passes
            Thread.Sleep(100);
            
            var elapsed = clock.Tick();
            
            Assert.Greater(elapsed.TotalHours, 0.0);
        }

        [Test]
        public void WorldClock_GetTimeOfDay_ReturnsHoursWithin24()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 1 // 1 minute = 1 day
            });
            
            var clock = new WorldClock(options);
            
            // Set to a specific time
            clock.SetWorldTime(12.5); // 12:30 PM
            
            var timeOfDay = clock.GetTimeOfDay();
            
            Assert.GreaterOrEqual(timeOfDay, 0.0);
            Assert.Less(timeOfDay, 24.0);
            Assert.AreEqual(12.5, timeOfDay, 0.1);
        }

        [Test]
        public void WorldClock_GetDay_ReturnsDayNumber()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 1
            });
            
            var clock = new WorldClock(options);
            
            // Set to day 5, 12:00 PM
            clock.SetWorldTime(5 * 24.0 + 12.0);
            
            var day = clock.GetDay();
            
            Assert.AreEqual(5, day);
        }

        [Test]
        public void WorldClock_RealTimeToGameTime_ConvertsCorrectly()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 24 // 24 real minutes = 1 game day
            });
            
            var clock = new WorldClock(options);
            
            var realTime = TimeSpan.FromMinutes(24);
            var gameTime = clock.RealTimeToGameTime(realTime);
            
            // 24 minutes real time = 1 day = 24 hours game time
            Assert.AreEqual(24.0, gameTime.TotalHours, 0.1);
        }

        [Test]
        public void WorldClock_GameTimeToRealTime_ConvertsCorrectly()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 24
            });
            
            var clock = new WorldClock(options);
            
            var gameTime = TimeSpan.FromHours(24); // 1 game day
            var realTime = clock.GameTimeToRealTime(gameTime);
            
            // 1 game day = 24 real minutes
            Assert.AreEqual(24.0, realTime.TotalMinutes, 0.1);
        }

        [Test]
        public void WorldClock_GetTimeUntilNextHour_ReturnsCorrectDuration()
        {
            var options = Options.Create(new SimulationOptions
            {
                DayLengthMinutes = 24
            });
            
            var clock = new WorldClock(options);
            
            // Set to 12:30 PM (12.5 hours)
            clock.SetWorldTime(12.5);
            
            var timeUntil = clock.GetTimeUntilNextHour();
            
            // Should be ~30 minutes until 1:00 PM
            Assert.Greater(timeUntil.TotalSeconds, 0);
        }
    }
}
