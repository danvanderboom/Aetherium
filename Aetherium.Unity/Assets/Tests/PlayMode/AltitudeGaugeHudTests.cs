using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class AltitudeGaugeHudTests
    {
        private GameObject? host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) { Object.DestroyImmediate(host); host = null; }
        }

        private AltitudeGaugeHud NewHud()
        {
            host = new GameObject("AltitudeGauge");
            return host.AddComponent<AltitudeGaugeHud>();
        }

        private static PerceptionLite FlyingFrame(int min, int max, int current)
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, current),
                FlightEnvelope = new FlightEnvelopeLite { MinBand = min, MaxBand = max, CurrentBand = current, State = "Airborne" },
                Visuals = new Dictionary<string, VisualLite>(),
            };
        }

        [Test]
        public void UpdateFrom_Flyer_ShowsGaugeWithRungs()
        {
            var hud = NewHud();
            hud.UpdateFrom(FlyingFrame(1, 5, 3));

            Assert.IsTrue(hud.IsVisible);
            Assert.AreEqual(5, hud.Rungs.Count);
            Assert.AreEqual(3, hud.CurrentBand);
            Assert.AreEqual(0.5f, hud.Normalized, 1e-4f, "Band 3 of [1,5] is halfway");
        }

        [Test]
        public void UpdateFrom_NonFlyer_HidesGauge()
        {
            var hud = NewHud();
            var frame = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                FlightEnvelope = null,
                Visuals = new Dictionary<string, VisualLite>(),
            };

            hud.UpdateFrom(frame);

            Assert.IsFalse(hud.IsVisible);
            Assert.AreEqual(0, hud.Rungs.Count);
        }

        [Test]
        public void UpdateFrom_LandingThenFlying_TogglesVisibility()
        {
            var hud = NewHud();

            hud.UpdateFrom(FlyingFrame(0, 4, 2));
            Assert.IsTrue(hud.IsVisible);

            var landed = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                FlightEnvelope = null,
                Visuals = new Dictionary<string, VisualLite>(),
            };
            hud.UpdateFrom(landed);
            Assert.IsFalse(hud.IsVisible);
            Assert.AreEqual(0, hud.Rungs.Count, "Rungs cleared when the gauge hides");
        }
    }
}
