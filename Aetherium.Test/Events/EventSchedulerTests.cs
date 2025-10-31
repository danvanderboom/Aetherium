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
	public class EventSchedulerTests
	{
		private static IOptions<SimulationOptions> CreateOptions(bool enableProceduralEvents = true)
		{
			return Options.Create(new SimulationOptions
			{
				EnableProceduralEvents = enableProceduralEvents
			});
		}

		[Test]
		public async Task ScheduleEvent_Disabled_ReturnsEmptyId()
		{
			var scheduler = new EventScheduler(CreateOptions(enableProceduralEvents: false));
			var id = await scheduler.ScheduleEventAsync("merchant_caravan", new Dictionary<string, object>(), 0.0);
			Assert.That(id, Is.Not.Null);
			Assert.That(Guid.TryParse(id, out _), Is.True);
			var events = await scheduler.GetScheduledEventsAsync();
			Assert.That(events.Count, Is.EqualTo(0));
		}

		[Test]
		public async Task ScheduleAtLocation_TriggersImmediatelyOnProcess()
		{
			var scheduler = new EventScheduler(CreateOptions());
			var before = await scheduler.GetScheduledEventsAsync();
			Assert.That(before.Count, Is.EqualTo(0));

			var id = await scheduler.ScheduleEventAtLocationAsync("merchant_caravan", new Dictionary<string, object>(), 10, 20, 0);
			var pending = await scheduler.GetScheduledEventsAsync();
			Assert.That(pending.Any(e => e.EventId == id), Is.True);

			await scheduler.ProcessScheduledEventsAsync(0.0, 0);

			var after = await scheduler.GetScheduledEventsAsync();
			Assert.That(after.Any(e => e.EventId == id), Is.False);
		}

		[Test]
		public async Task ScheduleForFuture_TriggersAtScheduledTime()
		{
			var scheduler = new EventScheduler(CreateOptions());
			var id = await scheduler.ScheduleEventAsync("monster_invasion", new Dictionary<string, object>(), 10.0);

			// Before time
			await scheduler.ProcessScheduledEventsAsync(5.0, 0);
			var pending = await scheduler.GetScheduledEventsAsync();
			Assert.That(pending.Any(e => e.EventId == id), Is.True);

			// At time
			await scheduler.ProcessScheduledEventsAsync(10.0, 0);
			var after = await scheduler.GetScheduledEventsAsync();
			Assert.That(after.Any(e => e.EventId == id), Is.False);
		}

		[Test]
		public async Task ScheduleRecurringEvent_ReschedulesAfterProcessing()
		{
			var scheduler = new EventScheduler(CreateOptions());
			var id = await scheduler.ScheduleRecurringEventAsync("merchant_caravan", new Dictionary<string, object>(), 2.0);

			// First process should trigger immediately and reschedule to now + interval
			await scheduler.ProcessScheduledEventsAsync(1.0, 0);
			var events = await scheduler.GetScheduledEventsAsync();
			var evt = events.FirstOrDefault(e => e.EventId == id);
			Assert.That(evt, Is.Not.Null);
			Assert.That(evt!.ScheduledGameTime, Is.GreaterThanOrEqualTo(3.0).Within(0.0001));
		}

		[Test]
		public async Task CancelEvent_RemovesFromSchedule()
		{
			var scheduler = new EventScheduler(CreateOptions());
			var id = await scheduler.ScheduleEventAsync("monster_invasion", new Dictionary<string, object>(), 5.0);
			var before = await scheduler.GetScheduledEventsAsync();
			Assert.That(before.Any(e => e.EventId == id), Is.True);

			var cancelled = await scheduler.CancelEventAsync(id);
			Assert.That(cancelled, Is.True);

			var after = await scheduler.GetScheduledEventsAsync();
			Assert.That(after.Any(e => e.EventId == id), Is.False);
		}
	}
}


