using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Server.Perception;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// P3-8a: the heat-trail export/import contract that lets grain-authoritative heat survive a
    /// cold start. Tested at the tracker level so it's deterministic and independent of the grain's
    /// clock wiring.
    /// </summary>
    [TestFixture]
    public class HeatTrailPersistenceTests
    {
        private static readonly DateTime T0 = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ExportImport_RoundTrips_LiveTrails()
        {
            var tracker = new HeatTrailTracker();
            var loc = new WorldLocation(5, 6, 0);
            tracker.RecordRaw("e1", loc, T0, 0.8, TimeSpan.FromSeconds(100));

            var asOf = T0.AddSeconds(10); // 10s of a 100s trail → still live
            var exported = tracker.ExportTrails(asOf);

            Assert.That(exported.Count, Is.EqualTo(1));
            var entry = exported[0];
            Assert.That(entry.Location, Is.EqualTo(loc));
            Assert.That(entry.EntityId, Is.EqualTo("e1"));
            Assert.That(entry.BaseIntensity, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(entry.Timestamp, Is.EqualTo(T0));
            Assert.That(entry.Duration, Is.EqualTo(TimeSpan.FromSeconds(100)));

            // Restoring into a fresh tracker reproduces the exact same live heat (fade preserved
            // because the original timestamp is carried through).
            var restored = new HeatTrailTracker();
            restored.ImportTrails(exported);

            var original = tracker.GetHeatAtLocation(loc, asOf);
            var afterRestore = restored.GetHeatAtLocation(loc, asOf);
            Assert.That(afterRestore, Is.GreaterThan(0.0));
            Assert.That(afterRestore, Is.EqualTo(original).Within(1e-9));
        }

        [Test]
        public void ExportTrails_DropsFullyFadedTrails()
        {
            var tracker = new HeatTrailTracker();
            var loc = new WorldLocation(1, 1, 0);
            tracker.RecordRaw("e1", loc, T0, 0.9, TimeSpan.FromSeconds(30));

            var asOf = T0.AddSeconds(60); // 60s ≥ 30s duration → fully faded
            var exported = tracker.ExportTrails(asOf);

            Assert.That(exported, Is.Empty);
        }

        [Test]
        public void ExportImport_PreservesMultipleTrailsAcrossLocations()
        {
            var tracker = new HeatTrailTracker();
            var a = new WorldLocation(2, 2, 0);
            var b = new WorldLocation(3, 4, 1);
            tracker.RecordRaw("e1", a, T0, 0.5, TimeSpan.FromSeconds(100));
            tracker.RecordRaw("e2", b, T0, 0.7, TimeSpan.FromSeconds(100));

            var asOf = T0.AddSeconds(5);
            var restored = new HeatTrailTracker();
            restored.ImportTrails(tracker.ExportTrails(asOf));

            Assert.That(restored.GetHeatAtLocation(a, asOf), Is.EqualTo(tracker.GetHeatAtLocation(a, asOf)).Within(1e-9));
            Assert.That(restored.GetHeatAtLocation(b, asOf), Is.EqualTo(tracker.GetHeatAtLocation(b, asOf)).Within(1e-9));
        }
    }
}
