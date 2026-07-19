using System.Collections.Generic;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class ChaikinSmoothingTests
    {
        private static List<Vector2> Square() => new List<Vector2>
        {
            new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2),
        };

        [Test]
        public void ZeroIterations_ReturnsSamePoints()
        {
            var loop = Square();
            var result = ChaikinSmoothing.Smooth(loop, 0);
            Assert.AreEqual(loop.Count, result.Count);
            for (int i = 0; i < loop.Count; i++)
                Assert.AreEqual(loop[i], result[i]);
        }

        [Test]
        public void EachIteration_DoublesVertexCount()
        {
            var loop = Square(); // 4 corners
            Assert.AreEqual(8, ChaikinSmoothing.Smooth(loop, 1).Count);
            Assert.AreEqual(16, ChaikinSmoothing.Smooth(loop, 2).Count);
            Assert.AreEqual(32, ChaikinSmoothing.Smooth(loop, 3).Count);
        }

        [Test]
        public void Smoothed_StaysWithinOriginalBounds()
        {
            var result = ChaikinSmoothing.Smooth(Square(), 3); // bbox [0,2] x [0,2]
            foreach (var p in result)
            {
                Assert.GreaterOrEqual(p.x, -1e-4f);
                Assert.LessOrEqual(p.x, 2f + 1e-4f);
                Assert.GreaterOrEqual(p.y, -1e-4f);
                Assert.LessOrEqual(p.y, 2f + 1e-4f);
            }
        }

        [Test]
        public void SmoothsIntegerCornerLoop()
        {
            var loop = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(0, 1),
            };
            var result = ChaikinSmoothing.Smooth(loop, 2);
            Assert.AreEqual(16, result.Count);
            foreach (var p in result)
            {
                Assert.GreaterOrEqual(p.x, -1e-4f);
                Assert.LessOrEqual(p.x, 1f + 1e-4f);
                Assert.GreaterOrEqual(p.y, -1e-4f);
                Assert.LessOrEqual(p.y, 1f + 1e-4f);
            }
        }

        [Test]
        public void DegenerateLoop_ReturnedUnchanged()
        {
            var loop = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 1) };
            var result = ChaikinSmoothing.Smooth(loop, 3);
            Assert.AreEqual(2, result.Count);
        }
    }
}
