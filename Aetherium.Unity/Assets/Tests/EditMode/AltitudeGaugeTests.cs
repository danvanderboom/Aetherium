using System.Linq;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class AltitudeGaugeTests
    {
        private static FlightEnvelopeLite Env(int min, int max, int current) =>
            new FlightEnvelopeLite { MinBand = min, MaxBand = max, CurrentBand = current };

        [Test]
        public void BuildRungs_NullEnvelope_IsEmpty()
        {
            Assert.IsEmpty(AltitudeGauge.BuildRungs(null));
        }

        [Test]
        public void BuildRungs_SpansEnvelopeTopFirst()
        {
            var rungs = AltitudeGauge.BuildRungs(Env(1, 5, 3));

            Assert.AreEqual(5, rungs.Count);
            Assert.AreEqual(5, rungs[0].band, "Top rung is MaxBand");
            Assert.AreEqual(1, rungs[rungs.Count - 1].band, "Bottom rung is MinBand");
        }

        [Test]
        public void BuildRungs_FlagsCurrentBandOnly()
        {
            var current = AltitudeGauge.BuildRungs(Env(1, 5, 3)).Where(r => r.isCurrent).ToList();

            Assert.AreEqual(1, current.Count);
            Assert.AreEqual(3, current[0].band);
        }

        [Test]
        public void NormalizedPosition_MapsCurrentBandTo0to1()
        {
            Assert.AreEqual(0f, AltitudeGauge.NormalizedPosition(Env(0, 4, 0)), 1e-4f, "MinBand -> 0");
            Assert.AreEqual(1f, AltitudeGauge.NormalizedPosition(Env(0, 4, 4)), 1e-4f, "MaxBand -> 1");
            Assert.AreEqual(0.5f, AltitudeGauge.NormalizedPosition(Env(0, 4, 2)), 1e-4f, "Midpoint -> 0.5");
        }

        [Test]
        public void NormalizedPosition_SingleBand_IsZero()
        {
            Assert.AreEqual(0f, AltitudeGauge.NormalizedPosition(Env(3, 3, 3)), 1e-4f);
            Assert.AreEqual(0f, AltitudeGauge.NormalizedPosition(null), 1e-4f);
        }
    }
}
