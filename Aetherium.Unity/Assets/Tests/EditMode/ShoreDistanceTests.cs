using System.Collections.Generic;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class ShoreDistanceTests
    {
        private static List<IReadOnlyList<Vector2>> SquareLoop()
        {
            var square = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 4), new Vector2(0, 4),
            };
            return new List<IReadOnlyList<Vector2>> { square };
        }

        [Test]
        public void DistanceToSegment_PerpendicularAndClamped()
        {
            var a = new Vector2(0, 0);
            var b = new Vector2(1, 0);
            Assert.AreEqual(0f, ShoreDistance.DistanceToSegment(new Vector2(0.5f, 0f), a, b), 1e-4f);
            Assert.AreEqual(1f, ShoreDistance.DistanceToSegment(new Vector2(0.5f, 1f), a, b), 1e-4f);
            Assert.AreEqual(1f, ShoreDistance.DistanceToSegment(new Vector2(2f, 0f), a, b), 1e-4f);
        }

        [Test]
        public void ToLoops_OnBoundary_IsZero()
        {
            Assert.AreEqual(0f, ShoreDistance.ToLoops(new Vector2(0f, 2f), SquareLoop()), 1e-4f);
        }

        [Test]
        public void ToLoops_Interior_IsDistanceToNearestEdge()
        {
            Assert.AreEqual(2f, ShoreDistance.ToLoops(new Vector2(2f, 2f), SquareLoop()), 1e-4f);
        }

        [Test]
        public void Normalized_ClampsOverShoreWidth()
        {
            Assert.AreEqual(0f, ShoreDistance.Normalized(0f, 1.5f), 1e-4f);
            Assert.AreEqual(1f, ShoreDistance.Normalized(1.5f, 1.5f), 1e-4f);
            Assert.AreEqual(1f, ShoreDistance.Normalized(3f, 1.5f), 1e-4f);
            Assert.AreEqual(0.5f, ShoreDistance.Normalized(0.75f, 1.5f), 1e-4f);
        }

        [Test]
        public void Normalized_ZeroWidth_IsOne()
        {
            Assert.AreEqual(1f, ShoreDistance.Normalized(0f, 0f), 1e-4f);
        }
    }
}
