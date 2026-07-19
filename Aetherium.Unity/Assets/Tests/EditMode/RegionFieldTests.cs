using System.Collections.Generic;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class RegionFieldTests
    {
        private static List<IReadOnlyList<Vector2>> Square(float min, float max)
        {
            var loop = new List<Vector2>
            {
                new Vector2(min, min), new Vector2(max, min),
                new Vector2(max, max), new Vector2(min, max),
            };
            return new List<IReadOnlyList<Vector2>> { loop };
        }

        [Test]
        public void Inside_Square()
        {
            var loops = Square(0, 4);
            Assert.IsTrue(RegionField.Inside(new Vector2(2, 2), loops));
            Assert.IsFalse(RegionField.Inside(new Vector2(-1, 2), loops));
            Assert.IsFalse(RegionField.Inside(new Vector2(5, 2), loops));
        }

        [Test]
        public void Inside_Hole_ExcludesIslandInterior()
        {
            // Outer CCW + hole CW -> the island interior is outside the water region.
            var outer = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(6, 0), new Vector2(6, 6), new Vector2(0, 6),
            };
            var hole = new List<Vector2>
            {
                new Vector2(2, 2), new Vector2(2, 4), new Vector2(4, 4), new Vector2(4, 2),
            };
            var loops = new List<IReadOnlyList<Vector2>> { outer, hole };
            Assert.IsTrue(RegionField.Inside(new Vector2(1, 1), loops), "water ring is inside");
            Assert.IsFalse(RegionField.Inside(new Vector2(3, 3), loops), "island interior is outside");
        }

        [Test]
        public void SignedDistance_PositiveInside_NegativeOutside()
        {
            var loops = Square(0, 4);
            Assert.Greater(RegionField.SignedDistance(new Vector2(2, 2), loops), 0f);
            Assert.AreEqual(2f, RegionField.SignedDistance(new Vector2(2, 2), loops), 1e-3f);
            Assert.Less(RegionField.SignedDistance(new Vector2(5, 2), loops), 0f);
            Assert.AreEqual(-1f, RegionField.SignedDistance(new Vector2(5, 2), loops), 1e-3f);
        }
    }
}
