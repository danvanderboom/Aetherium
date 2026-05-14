using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen.Algorithms.Graphs;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class MstAlgorithmTests
    {
        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static MinimumSpanningTree.Node N(int x, int y) =>
            new MinimumSpanningTree.Node(x, y);

        // ──────────────────────────────────────────────────────────────────────
        // Basic correctness
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ComputeMST_EmptyList_ReturnsEmpty()
        {
            var result = MinimumSpanningTree.ComputeMST(new List<MinimumSpanningTree.Node>());
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ComputeMST_SingleNode_ReturnsEmpty()
        {
            var result = MinimumSpanningTree.ComputeMST(new List<MinimumSpanningTree.Node> { N(0, 0) });
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ComputeMST_TwoNodes_ReturnsSingleEdge()
        {
            var a = N(0, 0);
            var b = N(3, 4); // distance = 5
            var result = MinimumSpanningTree.ComputeMST(new List<MinimumSpanningTree.Node> { a, b });

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Weight, Is.EqualTo(5.0).Within(1e-10));
        }

        [Test]
        public void ComputeMST_FourNodes_ProducesThreeEdges()
        {
            // square corners
            var nodes = new List<MinimumSpanningTree.Node>
            {
                N(0, 0), N(10, 0), N(10, 10), N(0, 10)
            };

            var mst = MinimumSpanningTree.ComputeMST(nodes);

            // A spanning tree of 4 nodes always has exactly 3 edges.
            Assert.That(mst.Count, Is.EqualTo(3));
        }

        [Test]
        public void ComputeMST_ProducesMinimalTotalWeight()
        {
            // Triangle where one edge is clearly longer — it must be excluded.
            var a = N(0, 0);
            var b = N(1, 0);  // distance a-b = 1
            var c = N(0, 1);  // distance a-c = 1, b-c = sqrt(2) ≈ 1.41

            var mst = MinimumSpanningTree.ComputeMST(new List<MinimumSpanningTree.Node> { a, b, c });
            Assert.That(mst.Count, Is.EqualTo(2));

            // Total weight should be 2 (the two unit edges), not 1 + sqrt(2).
            double totalWeight = mst.Sum(e => e.Weight);
            Assert.That(totalWeight, Is.EqualTo(2.0).Within(1e-10));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Disconnected graph (forest)
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ComputeMST_DisconnectedGraph_ReturnsForest()
        {
            // Two clusters far apart — one edge per cluster needed.
            // Use infinite weight function to simulate no cross-cluster edges.
            var a = N(0, 0);
            var b = N(1, 0);
            var c = N(1000, 0);
            var d = N(1001, 0);

            double InfBetweenClusters(MinimumSpanningTree.Node x, MinimumSpanningTree.Node y)
            {
                bool xLeft = x.X < 100;
                bool yLeft = y.X < 100;
                if (xLeft != yLeft)
                    return double.PositiveInfinity;
                return x.DistanceTo(y);
            }

            var mst = MinimumSpanningTree.ComputeMST(
                new List<MinimumSpanningTree.Node> { a, b, c, d },
                InfBetweenClusters);

            // Each cluster of 2 needs 1 edge → total 2 edges (forest, not tree).
            Assert.That(mst.Count, Is.EqualTo(2));
        }

        [Test]
        public void ComputeMST_AllInfiniteWeights_ReturnsEmptyForest()
        {
            var nodes = Enumerable.Range(0, 5)
                .Select(i => N(i * 10, 0))
                .ToList();

            var mst = MinimumSpanningTree.ComputeMST(nodes, (_, __) => double.PositiveInfinity);
            Assert.That(mst, Is.Empty, "No finite-weight edges → no tree edges.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Extra-edge variant
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void ComputeMSTWithExtraEdges_ZeroExtraEdge_ReturnsMSTOnly()
        {
            var nodes = new List<MinimumSpanningTree.Node>
            {
                N(0, 0), N(5, 0), N(5, 5), N(0, 5)
            };

            var result = MinimumSpanningTree.ComputeMSTWithExtraEdges(nodes, 0.0, new Random(42));
            Assert.That(result.Count, Is.EqualTo(3)); // MST of 4 nodes = 3 edges
        }

        [Test]
        public void ComputeMSTWithExtraEdges_WithExtraEdges_HasMoreEdgesThanMST()
        {
            var nodes = Enumerable.Range(0, 8)
                .Select(i => N(i * 10, 0))
                .ToList();

            // 7 MST edges. 1.0 ratio → 7 extra → total up to 14 (capped by available non-MST edges).
            var result = MinimumSpanningTree.ComputeMSTWithExtraEdges(nodes, 1.0, new Random(1));
            Assert.That(result.Count, Is.GreaterThan(7));
        }

        [Test]
        public void ComputeMSTWithExtraEdges_NoDuplicateEdges()
        {
            var nodes = Enumerable.Range(0, 6)
                .Select(i => N(i * 5, 0))
                .ToList();

            var result = MinimumSpanningTree.ComputeMSTWithExtraEdges(nodes, 0.5, new Random(99));

            // Use a canonical string key (min-X first) to detect any repeated edges.
            var edgeKeys = new HashSet<string>();
            foreach (var e in result)
            {
                // Canonical form: the endpoint with the smaller X comes first.
                bool fromFirst = e.From.X <= e.To.X;
                int ax = fromFirst ? e.From.X : e.To.X;
                int ay = fromFirst ? e.From.Y : e.To.Y;
                int bx = fromFirst ? e.To.X   : e.From.X;
                int by = fromFirst ? e.To.Y   : e.From.Y;
                string key = $"{ax},{ay}-{bx},{by}";
                Assert.That(edgeKeys.Add(key), Is.True, $"Duplicate edge detected: {key}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Performance sanity (should complete in < 1 second for 100 nodes)
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        [Timeout(1000)]
        public void ComputeMST_LargeGraph_CompletesInTime()
        {
            var nodes = Enumerable.Range(0, 100)
                .Select(i => N(i % 10 * 10, i / 10 * 10))
                .ToList();

            var mst = MinimumSpanningTree.ComputeMST(nodes);
            Assert.That(mst.Count, Is.EqualTo(99));
        }
    }
}
