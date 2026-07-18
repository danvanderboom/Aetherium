extern alias Console;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Model;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 5.3 of add-adaptive-depth-visualization: the console altitude gauge. A flying player gets a
    /// vertical ladder spanning [MinBand, MaxBand] (top band first) with the current band flagged; a non-flyer
    /// gets nothing. Verified through the pure builder, like the level ribbon.
    /// </summary>
    public class ConsoleAltitudeGaugeTests
    {
        private static ClientConsoleMapView ViewWithEnvelope(FlightEnvelopeDto? env)
        {
            var p = new PerceptionDto { PlayerLocation = new WorldLocationDto(0, 0, 0), FlightEnvelope = env };
            var view = new ClientConsoleMapView(new Point(0, 0), new Size(20, 10), hasFrame: false);
            view.Perception = p;
            view.WorldLocation = p.PlayerLocation;
            return view;
        }

        [Test]
        public void Gauge_EmptyWhenNotFlying()
        {
            var view = ViewWithEnvelope(null);
            Assert.IsEmpty(view.BuildAltitudeGauge());
            Assert.IsEmpty(view.RenderAltitudeGaugeLines());
        }

        [Test]
        public void Gauge_SpansEnvelopeTopFirst()
        {
            var view = ViewWithEnvelope(new FlightEnvelopeDto { MinBand = 1, MaxBand = 5, CurrentBand = 3, State = "Airborne" });
            var rungs = view.BuildAltitudeGauge();

            Assert.AreEqual(5, rungs.Count, "One rung per band 1..5");
            Assert.AreEqual(5, rungs[0].band, "Top rung is MaxBand");
            Assert.AreEqual(1, rungs[rungs.Count - 1].band, "Bottom rung is MinBand");
            for (int i = 1; i < rungs.Count; i++)
                Assert.Less(rungs[i].band, rungs[i - 1].band, "Bands descend");
        }

        [Test]
        public void Gauge_FlagsCurrentBandOnly()
        {
            var view = ViewWithEnvelope(new FlightEnvelopeDto { MinBand = 1, MaxBand = 5, CurrentBand = 3 });
            var current = view.BuildAltitudeGauge().Where(r => r.isCurrent).ToList();

            Assert.AreEqual(1, current.Count);
            Assert.AreEqual(3, current[0].band);
        }

        [Test]
        public void RenderLines_MarkCurrentBand()
        {
            var view = ViewWithEnvelope(new FlightEnvelopeDto { MinBand = 0, MaxBand = 2, CurrentBand = 1 });
            var lines = view.RenderAltitudeGaugeLines();

            Assert.AreEqual(3, lines.Count);
            Assert.IsTrue(lines[0].StartsWith("|"), "Band 2 is not current");
            Assert.IsTrue(lines[1].StartsWith(">"), "Band 1 is the current band");
            Assert.IsTrue(lines[2].StartsWith("|"), "Band 0 is not current");
        }

        [Test]
        public void Gauge_HandlesNegativeBands()
        {
            var view = ViewWithEnvelope(new FlightEnvelopeDto { MinBand = -4, MaxBand = 2, CurrentBand = -2 });
            var rungs = view.BuildAltitudeGauge();

            Assert.AreEqual(7, rungs.Count, "Bands -4..2 inclusive");
            Assert.AreEqual(2, rungs[0].band);
            Assert.AreEqual(-4, rungs[rungs.Count - 1].band);
            Assert.AreEqual(-2, rungs.Single(r => r.isCurrent).band);
        }
    }
}
