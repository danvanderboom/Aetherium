using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Simulation;
using Aetherium.Server.Events;

namespace Aetherium.Test.Simulation
{
    /// <summary>
    /// Covers P1-13: the shared simulation singletons (WorldClock/WeatherSystem/EventScheduler)
    /// must tolerate concurrent access without throwing or corrupting state, WorldClock.Tick()
    /// must not have its elapsed time stolen by interleaved reads, and weather transition cadence
    /// must follow game time rather than the number of update calls.
    /// </summary>
    [TestFixture]
    public class SimulationThreadSafetyTests
    {
        // ---- WorldClock -------------------------------------------------------------------

        [Test]
        public void WorldClock_Tick_ReflectsFullIntervalDespiteInterleavedReads()
        {
            var clock = new WorldClock(Options.Create(new SimulationOptions { DayLengthMinutes = 1 }));

            // Baseline tick, then measure the interval up to the next tick.
            clock.Tick();
            var sw = Stopwatch.StartNew();

            Thread.Sleep(50);
            // Interleave several reads — each advances the clock's internal accumulation.
            for (int i = 0; i < 10; i++)
            {
                clock.GetTimeOfDay();
                clock.GetDay();
                Thread.Sleep(5);
            }
            // No sleep between the last read and the tick: under the old design Tick() would
            // return only the sliver since the last read (~0). Under the fix it returns the
            // full game time accrued since the previous Tick().
            var elapsed2 = clock.Tick();
            sw.Stop();

            var expectedFullInterval = clock.RealTimeToGameTime(sw.Elapsed);
            Assert.That(elapsed2.TotalHours, Is.GreaterThan(expectedFullInterval.TotalHours * 0.5),
                "Tick() elapsed should reflect the whole inter-tick interval, not just the time since the last read.");
        }

        [Test]
        public void WorldClock_ConcurrentAccess_DoesNotThrowAndStaysFinite()
        {
            var clock = new WorldClock(Options.Create(new SimulationOptions { DayLengthMinutes = 1 }));

            Assert.DoesNotThrow(() =>
            {
                Parallel.For(0, 20_000, i =>
                {
                    clock.Tick();
                    clock.GetTimeOfDay();
                    clock.GetDay();
                    clock.GetTotalGameTimeHours();
                    clock.GetTimeUntilNextHour();
                    if (i % 500 == 0) clock.SetWorldTime(i % 240);
                });
            });

            Assert.That(double.IsFinite(clock.GetTotalGameTimeHours()), Is.True);
        }

        // ---- WeatherSystem ----------------------------------------------------------------

        [Test]
        public void WeatherSystem_FixedGameTime_NeverTransitions_RegardlessOfCallCount()
        {
            var weather = new WeatherSystem(Options.Create(new SimulationOptions { EnableWeather = true }));
            const string region = "region:0,0,0";

            // A fresh region's state is created with LastChangeTime == the update's timeOfDay, so
            // if game time never advances, elapsed game hours stay 0 and no transition can fire —
            // no matter how many times the region is ticked.
            for (int i = 0; i < 1000; i++)
            {
                weather.UpdateWeather(region, 10.0, 0, "spring");
            }

            Assert.That(weather.GetWeather(region), Is.EqualTo(WeatherType.Clear),
                "Weather cadence must follow game time — repeated updates at the same game time cannot transition.");
        }

        [Test]
        public void WeatherSystem_ConcurrentAccess_DoesNotThrow()
        {
            var weather = new WeatherSystem(Options.Create(new SimulationOptions { EnableWeather = true }));

            Assert.DoesNotThrow(() =>
            {
                Parallel.For(0, 5_000, i =>
                {
                    var region = "region:" + (i % 20);
                    weather.UpdateWeather(region, i % 24, i / 24, i % 2 == 0 ? "winter" : "summer");
                    if (i % 7 == 0) weather.SetWeather(region, WeatherType.Cloudy);
                    var w = weather.GetWeather(region);
                    Assert.That(Enum.IsDefined(typeof(WeatherType), w), Is.True);
                });
            });
        }

        // ---- EventScheduler ---------------------------------------------------------------

        [Test]
        public void EventScheduler_ConcurrentAccess_DoesNotThrow()
        {
            var scheduler = new EventScheduler(Options.Create(new SimulationOptions { EnableProceduralEvents = true }));

            Assert.DoesNotThrowAsync(async () =>
            {
                var tasks = new List<Task>();
                for (int t = 0; t < 16; t++)
                {
                    int worker = t;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int i = 0; i < 200; i++)
                        {
                            var id = await scheduler.ScheduleEventAsync(
                                "merchant_caravan", new Dictionary<string, object>(), (worker + i) % 24);
                            await scheduler.GetScheduledEventsAsync();
                            await scheduler.ProcessScheduledEventsAsync((worker + i) % 24, 0);
                            if (i % 3 == 0) await scheduler.CancelEventAsync(id);
                        }
                    }));
                }
                await Task.WhenAll(tasks);
            });
        }
    }
}
