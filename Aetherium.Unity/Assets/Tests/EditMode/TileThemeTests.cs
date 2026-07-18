using System.Collections.Generic;
using Aetherium.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class TileThemeTests
    {
        [Test]
        public void ColorFor_IsDeterministic()
        {
            Assert.AreEqual(TileTheme.ColorFor("stone"), TileTheme.ColorFor("stone"));
            Assert.AreEqual(TileTheme.ColorFor("mystery-xyz"), TileTheme.ColorFor("mystery-xyz"));
        }

        [Test]
        public void ColorFor_IsCaseInsensitive()
        {
            Assert.AreEqual(TileTheme.ColorFor("Stone"), TileTheme.ColorFor("stone"));
            Assert.AreEqual(TileTheme.ColorFor("WATER"), TileTheme.ColorFor("water"));
        }

        [Test]
        public void ColorFor_KnownTerrains_AreDistinct()
        {
            Assert.AreNotEqual(TileTheme.ColorFor("stone"), TileTheme.ColorFor("water"));
            Assert.AreNotEqual(TileTheme.ColorFor("grass"), TileTheme.ColorFor("sand"));
        }

        [Test]
        public void ColorFor_UnknownId_IsInGamut()
        {
            var c = TileTheme.ColorFor("subway-42");
            Assert.GreaterOrEqual(c.r, 0f); Assert.LessOrEqual(c.r, 1f);
            Assert.GreaterOrEqual(c.g, 0f); Assert.LessOrEqual(c.g, 1f);
            Assert.GreaterOrEqual(c.b, 0f); Assert.LessOrEqual(c.b, 1f);
        }

        [Test]
        public void ColorFor_NullOrEmpty_ReturnsGrayFallback()
        {
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f), TileTheme.ColorFor(null));
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f), TileTheme.ColorFor(string.Empty));
        }

        [Test]
        public void ColorFor_UnknownIds_AreMostlyDistinct()
        {
            // Hashed hues can rarely collide mod 360; require near-total distinctness
            // across a spread of ids rather than a guarantee for every single pair.
            var ids = new[] { "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8" };
            var colors = new HashSet<Color>();
            foreach (var id in ids)
                colors.Add(TileTheme.ColorFor(id));

            Assert.GreaterOrEqual(colors.Count, ids.Length - 1,
                "Hashed tile colors should be mostly distinct");
        }
    }
}
