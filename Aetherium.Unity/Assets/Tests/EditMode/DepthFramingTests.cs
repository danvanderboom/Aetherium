using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class DepthFramingTests
    {
        private static PerceptionLite Frame(params int[] bands)
        {
            var visuals = new Dictionary<string, VisualLite>();
            int i = 0;
            foreach (var z in bands)
                visuals[$"{i},0,{z}"] = new VisualLite(new WorldLocationLite(i++, 0, z), "stone", 1.0);

            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                Visuals = visuals,
            };
        }

        [Test]
        public void OccupiedBandCount_CountsDistinctBandsIncludingPlayer()
        {
            Assert.AreEqual(3, DepthFraming.OccupiedBandCount(Frame(0, 1, 1, -1)),
                "Distinct bands {0,1,-1} counted once each");
        }

        [Test]
        public void OccupiedBandCount_EmptyFrame_CountsPlayerBand()
        {
            Assert.AreEqual(1, DepthFraming.OccupiedBandCount(Frame()));
            Assert.AreEqual(0, DepthFraming.OccupiedBandCount(null));
        }

        [Test]
        public void OrthographicSizeFor_FlatFrame_IsBaseSize()
        {
            Assert.AreEqual(DepthFraming.DefaultBaseSize, DepthFraming.OrthographicSizeFor(1), 1e-4f);
            Assert.AreEqual(DepthFraming.DefaultBaseSize, DepthFraming.OrthographicSizeFor(0), 1e-4f);
        }

        [Test]
        public void OrthographicSizeFor_MoreBands_PullsBack()
        {
            float flat = DepthFraming.OrthographicSizeFor(1);
            float tall = DepthFraming.OrthographicSizeFor(4);
            Assert.Greater(tall, flat, "More occupied bands widen the framing");
        }

        [Test]
        public void OrthographicSizeFor_ClampedToMax()
        {
            Assert.AreEqual(DepthFraming.DefaultMaxSize, DepthFraming.OrthographicSizeFor(100), 1e-4f);
        }

        [Test]
        public void ShouldSurfaceCrossSection_HonorsThreshold()
        {
            int t = DepthFraming.DefaultEscalationThreshold;
            Assert.IsFalse(DepthFraming.ShouldSurfaceCrossSection(t - 1));
            Assert.IsTrue(DepthFraming.ShouldSurfaceCrossSection(t));
            Assert.IsTrue(DepthFraming.ShouldSurfaceCrossSection(t + 3));
        }
    }
}
