using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class TerrainLightingTests
    {
        [Test]
        public void FullLight_NeutralTint_LeavesColorUnchanged()
        {
            var c = TerrainLighting.Modulate(new Color(0.8f, 0.6f, 0.4f, 1f), 1.0, TerrainLighting.NeutralTint);
            Assert.AreEqual(0.8f, c.r, 1e-4f);
            Assert.AreEqual(0.6f, c.g, 1e-4f);
            Assert.AreEqual(0.4f, c.b, 1e-4f);
        }

        [Test]
        public void HalfLight_HalvesBrightness()
        {
            var c = TerrainLighting.Modulate(new Color(0.8f, 0.6f, 0.4f, 1f), 0.5, TerrainLighting.NeutralTint);
            Assert.AreEqual(0.4f, c.r, 1e-4f);
            Assert.AreEqual(0.3f, c.g, 1e-4f);
            Assert.AreEqual(0.2f, c.b, 1e-4f);
        }

        [Test]
        public void AmbientTint_ShiftsTowardTintColor()
        {
            var sunset = new Color(1f, 0.6f, 0.4f, 1f);
            var c = TerrainLighting.Modulate(Color.white, 1.0, sunset);
            Assert.AreEqual(1f, c.r, 1e-4f);
            Assert.AreEqual(0.6f, c.g, 1e-4f);
            Assert.AreEqual(0.4f, c.b, 1e-4f);
        }

        [Test]
        public void LightLevel_IsClamped()
        {
            var over = TerrainLighting.Modulate(new Color(0.5f, 0.5f, 0.5f, 1f), 5.0, TerrainLighting.NeutralTint);
            Assert.AreEqual(0.5f, over.r, 1e-4f, "light > 1 clamps to 1");
            var under = TerrainLighting.Modulate(new Color(0.5f, 0.5f, 0.5f, 1f), -3.0, TerrainLighting.NeutralTint);
            Assert.AreEqual(0f, under.r, 1e-4f, "light < 0 clamps to 0");
        }

        [Test]
        public void AlphaIsPreserved()
        {
            var c = TerrainLighting.Modulate(new Color(1f, 1f, 1f, 0.5f), 0.5, TerrainLighting.NeutralTint);
            Assert.AreEqual(0.5f, c.a, 1e-4f);
        }
    }
}
