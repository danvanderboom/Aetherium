using System.Collections.Generic;
using Aetherium.Unity.Rendering.Water;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class WaterMeshBuilderTests
    {
        private static List<(int, int)> Block(int w, int h)
        {
            var cells = new List<(int, int)>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    cells.Add((x, y));
            return cells;
        }

        [Test]
        public void Empty_ProducesEmptyMesh()
        {
            var data = WaterMeshBuilder.Build(new List<(int, int)>());
            Assert.IsTrue(data.IsEmpty);
            Assert.AreEqual(0, data.Triangles.Count);
        }

        [Test]
        public void Lake_ProducesSubdividedValidMesh()
        {
            var data = WaterMeshBuilder.Build(Block(3, 3), smoothIterations: 1, subdivisions: 2);
            Assert.Greater(data.Vertices.Count, 9, "sub-grid yields more vertices than cells");
            Assert.AreEqual(0, data.Triangles.Count % 3);
            Assert.Greater(data.Triangles.Count, 0);
            foreach (var idx in data.Triangles)
            {
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, data.Vertices.Count);
            }
            Assert.AreEqual(data.Vertices.Count, data.ShoreUv.Count);
            Assert.AreEqual(data.Vertices.Count, data.WorldUv.Count);
        }

        [Test]
        public void Lake_HasInteriorAndShoreVertices()
        {
            var data = WaterMeshBuilder.Build(Block(4, 4), smoothIterations: 1, subdivisions: 2);
            float maxSigned = float.NegativeInfinity;
            float minSigned = float.PositiveInfinity;
            foreach (var uv in data.ShoreUv)
            {
                if (uv.x > maxSigned) maxSigned = uv.x;
                if (uv.x < minSigned) minSigned = uv.x;
            }
            Assert.Greater(maxSigned, 0.5f, "interior water vertices exist (deep, positive signed distance)");
            Assert.Less(minSigned, 0.05f, "shore/near-shore vertices exist (signed distance at or below zero)");
        }

        [Test]
        public void Lake_MeshStaysWithinDilatedBounds()
        {
            var data = WaterMeshBuilder.Build(Block(3, 3), subdivisions: 2, cellSize: 1f);
            // cells 0..2 -> corners 0..3, dilated by one cell -> [-1, 4].
            foreach (var v in data.Vertices)
            {
                Assert.GreaterOrEqual(v.x, -1f - 1e-3f);
                Assert.LessOrEqual(v.x, 4f + 1e-3f);
                Assert.GreaterOrEqual(v.y, -1f - 1e-3f);
                Assert.LessOrEqual(v.y, 4f + 1e-3f);
            }
        }
    }
}
