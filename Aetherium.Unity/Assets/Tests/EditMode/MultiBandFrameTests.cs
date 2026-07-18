using System.Collections.Generic;
using System.IO;
using Aetherium.Unity.Model;
using Aetherium.Unity.Rendering;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    /// <summary>
    /// Verifies that the DTO→PerceptionLite mapping carries multi-Z slabs through
    /// the JSON mock path (Task 3.4). Uses the interchange fixture so the unfinished
    /// live SignalR path never blocks visualization work.
    /// </summary>
    public class MultiBandFrameTests
    {
        private static PerceptionLite LoadInterchange()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames", "interchange-frame.json");
            Assert.IsTrue(File.Exists(path), $"interchange-frame.json missing at {path}");

            var json = File.ReadAllText(path);
            var perception = JsonConvert.DeserializeObject<PerceptionLite>(json);
            Assert.IsNotNull(perception, "Interchange frame should deserialize");
            return perception!;
        }

        [Test]
        public void InterchangeFrame_SpansMultipleBands()
        {
            var perception = LoadInterchange();

            var bands = new HashSet<int>();
            foreach (var visual in perception.Visuals.Values)
                bands.Add(visual.Location.Z);

            Assert.GreaterOrEqual(bands.Count, 3, "Interchange fixture should span at least 3 bands");
            Assert.IsTrue(bands.Contains(perception.PlayerLocation.Z), "Focus band should be represented");
        }

        [Test]
        public void InterchangeFrame_KeysMatchRelativeLocations()
        {
            var perception = LoadInterchange();

            foreach (var kv in perception.Visuals)
            {
                var loc = kv.Value.Location;
                Assert.AreEqual($"{loc.X},{loc.Y},{loc.Z}", kv.Key,
                    "Visual dictionary key must match its relative (x,y,z) location");
            }
        }

        [Test]
        public void InterchangeFrame_DepthFalloff_FocusOpaqueOthersFaded()
        {
            var perception = LoadInterchange();
            int focus = perception.PlayerLocation.Z;

            foreach (var visual in perception.Visuals.Values)
            {
                int dz = Mathf.Abs(visual.Location.Z - focus);
                float alpha = DepthShading.AlphaForDepth(dz);

                if (dz == 0)
                    Assert.AreEqual(1f, alpha, 1e-5f, "Focus-band cells must be opaque");
                else
                    Assert.Less(alpha, 1f, "Off-focus cells must be faded");
            }
        }
    }
}
