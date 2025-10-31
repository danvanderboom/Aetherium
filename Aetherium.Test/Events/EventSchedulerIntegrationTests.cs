using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.Options;
using Aetherium.Server.Events;
using Aetherium.Server.Simulation;

namespace Aetherium.Test.Events
{
    [TestFixture]
    public class EventSchedulerIntegrationTests
    {
        [Test]
        public async Task EventScheduler_ProcessEvents_TriggersAtCorrectTime()
        {
            var scheduler = new EventScheduler(Options.Create(new SimulationOptions
            {
                EnableProceduralEvents = true
            }));

            var triggered = false;
            scheduler.RegisterHandler("test_event", new TestEventHandler(() => triggered = true));

            // Schedule event for 5.0 game hours
            var id = await scheduler.ScheduleEventAsync("test_event", new Dictionary<string, object>(), 5.0);

            // Before time - should not trigger
            await scheduler.ProcessScheduledEventsAsync(3.0, 0);
            Assert.That(triggered, Is.False);

            // At time - should trigger
            await scheduler.ProcessScheduledEventsAsync(5.0, 0);
            Assert.That(triggered, Is.True);
        }

        [Test]
        public async Task EventScheduler_RecurringEvents_RescheduleAfterProcessing()
        {
            var scheduler = new EventScheduler(Options.Create(new SimulationOptions
            {
                EnableProceduralEvents = true
            }));

            var triggerCount = 0;
            scheduler.RegisterHandler("recurring_event", new TestEventHandler(() => triggerCount++));

            // Schedule recurring event every 2 hours
            var id = await scheduler.ScheduleRecurringEventAsync("recurring_event", new Dictionary<string, object>(), 2.0);

            // First trigger at 1.0 (immediate)
            await scheduler.ProcessScheduledEventsAsync(1.0, 0);
            Assert.That(triggerCount, Is.EqualTo(1));

            // Second trigger should be scheduled for 3.0
            var events = await scheduler.GetScheduledEventsAsync();
            var evt = events.FirstOrDefault(e => e.EventId == id);
            Assert.That(evt, Is.Not.Null);
            Assert.That(evt!.ScheduledGameTime, Is.GreaterThanOrEqualTo(3.0));

            // Process again at 3.0
            await scheduler.ProcessScheduledEventsAsync(3.0, 0);
            Assert.That(triggerCount, Is.EqualTo(2));
        }

        [Test]
        public async Task EventScheduler_LocationBasedEvents_CanBeScheduled()
        {
            var scheduler = new EventScheduler(Options.Create(new SimulationOptions
            {
                EnableProceduralEvents = true
            }));

            var triggered = false;
            scheduler.RegisterHandler("location_event", new TestEventHandler(() => triggered = true));

            // Schedule event at specific location
            var id = await scheduler.ScheduleEventAtLocationAsync("location_event", new Dictionary<string, object>(), 10, 20, 0);

            var events = await scheduler.GetScheduledEventsAsync();
            var evt = events.FirstOrDefault(e => e.EventId == id);

            Assert.That(evt, Is.Not.Null);
            Assert.That(evt!.X, Is.EqualTo(10));
            Assert.That(evt!.Y, Is.EqualTo(20));
            Assert.That(evt!.Z, Is.EqualTo(0));

            // Process immediately (ScheduledGameTime = 0.0 triggers immediately)
            await scheduler.ProcessScheduledEventsAsync(0.0, 0);
            Assert.That(triggered, Is.True);
        }

        private class TestEventHandler : IEventHandler
        {
            private readonly Action _onTrigger;

            public TestEventHandler(Action onTrigger)
            {
                _onTrigger = onTrigger;
            }

            public Task HandleEventAsync(ScheduledEvent scheduledEvent, double currentGameTime, int day)
            {
                _onTrigger();
                return Task.CompletedTask;
            }
        }
    }
}

