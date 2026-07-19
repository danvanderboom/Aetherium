using Aetherium.Unity.Rendering;
using NUnit.Framework;

namespace Aetherium.Unity.Tests
{
    public class DepthShadingTests
    {
        [Test]
        public void AlphaForDepth_FocusBand_IsFullyOpaque()
        {
            Assert.AreEqual(1f, DepthShading.AlphaForDepth(0), 1e-5f);
        }

        [Test]
        public void AlphaForDepth_FallsOffMonotonically()
        {
            float a0 = DepthShading.AlphaForDepth(0);
            float a1 = DepthShading.AlphaForDepth(1);
            float a2 = DepthShading.AlphaForDepth(2);
            float a3 = DepthShading.AlphaForDepth(3);

            Assert.Greater(a0, a1);
            Assert.Greater(a1, a2);
            Assert.GreaterOrEqual(a2, a3);
        }

        [Test]
        public void AlphaForDepth_IsSymmetricInSign()
        {
            Assert.AreEqual(DepthShading.AlphaForDepth(2), DepthShading.AlphaForDepth(-2), 1e-6f);
        }

        [Test]
        public void AlphaForDepth_NeverBelowFloor()
        {
            float a = DepthShading.AlphaForDepth(100, DepthShading.DefaultFalloff, 0.2f);
            Assert.GreaterOrEqual(a, 0.2f);
        }

        [Test]
        public void AlphaForDepth_MatchesConsoleDepthFactor()
        {
            // Console composite uses DepthFactor = 1 / (1 + 0.5·|dz|); at dz = 2 that
            // is exactly 0.5. Keeping parity means both renderers cue depth alike.
            Assert.AreEqual(0.5f, DepthShading.AlphaForDepth(2), 1e-5f);
        }

        [Test]
        public void SortingOrder_HigherBandDrawsAbove()
        {
            Assert.Greater(DepthShading.SortingOrderForBand(2), DepthShading.SortingOrderForBand(0));
            Assert.Greater(DepthShading.SortingOrderForBand(0), DepthShading.SortingOrderForBand(-1));
        }
    }
}
