using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class DepthDirectorTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private (DepthDirector director, FollowCamera cam, CrossSectionOverlay overlay) NewRig()
        {
            var camGo = new GameObject("Cam");
            camGo.AddComponent<Camera>();
            var cam = camGo.AddComponent<FollowCamera>();
            spawned.Add(camGo);

            var overlayGo = new GameObject("Overlay");
            var overlay = overlayGo.AddComponent<CrossSectionOverlay>();
            spawned.Add(overlayGo);

            var dirGo = new GameObject("Director");
            var director = dirGo.AddComponent<DepthDirector>();
            spawned.Add(dirGo);

            director.SetDependencies(cam, overlay);
            return (director, cam, overlay);
        }

        private static PerceptionLite FlatFrame()
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["0,0,0"] = new VisualLite(new WorldLocationLite(0, 0, 0), "road", 1.0),
                    ["1,0,0"] = new VisualLite(new WorldLocationLite(1, 0, 0), "road", 1.0),
                },
            };
        }

        private static PerceptionLite TallFrame()
        {
            return new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(0, 0, 0),
                Visuals = new Dictionary<string, VisualLite>
                {
                    ["0,0,0"] = new VisualLite(new WorldLocationLite(0, 0, 0), "road", 1.0),
                    ["0,0,1"] = new VisualLite(new WorldLocationLite(0, 0, 1), "metal", 1.0),
                    ["0,0,2"] = new VisualLite(new WorldLocationLite(0, 0, 2), "metal", 1.0),
                    ["0,0,3"] = new VisualLite(new WorldLocationLite(0, 0, 3), "metal", 1.0),
                },
            };
        }

        [Test]
        public void OnPerception_FlatFrame_ClosesFramingAndHidesCrossSection()
        {
            var (director, cam, overlay) = NewRig();

            director.OnPerception(FlatFrame());

            Assert.AreEqual(1, director.LastBandCount);
            Assert.AreEqual(DepthFraming.OrthographicSizeFor(1), cam.OrthographicSize, 1e-4f);
            Assert.IsFalse(director.CrossSectionActive);
            Assert.IsFalse(overlay.IsShowing);
        }

        [Test]
        public void OnPerception_TallFrame_PullsBackAndSurfacesCrossSection()
        {
            var (director, cam, overlay) = NewRig();

            director.OnPerception(TallFrame());

            Assert.AreEqual(4, director.LastBandCount);
            Assert.AreEqual(DepthFraming.OrthographicSizeFor(4), cam.OrthographicSize, 1e-4f);
            Assert.Greater(cam.OrthographicSize, DepthFraming.OrthographicSizeFor(1), "Tall frame pulls the camera back");
            Assert.IsTrue(director.CrossSectionActive);
            Assert.IsTrue(overlay.IsShowing);
            Assert.Greater(overlay.RenderedCellCount, 0, "Surfaced cross-section draws its schematic");
        }

        [Test]
        public void OnPerception_FlatAfterTall_CollapsesFramingAndHides()
        {
            var (director, cam, overlay) = NewRig();

            director.OnPerception(TallFrame());
            director.OnPerception(FlatFrame());

            Assert.AreEqual(1, director.LastBandCount);
            Assert.AreEqual(DepthFraming.OrthographicSizeFor(1), cam.OrthographicSize, 1e-4f);
            Assert.IsFalse(director.CrossSectionActive);
            Assert.IsFalse(overlay.IsShowing);
        }
    }
}
