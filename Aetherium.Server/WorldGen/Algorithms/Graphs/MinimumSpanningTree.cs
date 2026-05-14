using System;
using System.Collections.Generic;

namespace Aetherium.WorldGen.Algorithms.Graphs
{
    /// <summary>
    /// Minimum Spanning Tree using PriorityQueue-backed Prim's algorithm.
    /// Edges with non-finite weights are treated as missing — disconnected inputs return a forest.
    /// </summary>
    public static class MinimumSpanningTree
    {
        public class Node
        {
            public int X { get; set; }
            public int Y { get; set; }
            public object? Data { get; set; }

            public Node(int x, int y, object? data = null)
            {
                X = x;
                Y = y;
                Data = data;
            }

            public double DistanceTo(Node other)
            {
                int dx = X - other.X;
                int dy = Y - other.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public class Edge
        {
            public Node From { get; set; }
            public Node To { get; set; }
            public double Weight { get; set; }

            public Edge(Node from, Node to, double weight)
            {
                From = from;
                To = to;
                Weight = weight;
            }
        }

        /// <summary>
        /// Computes a minimum spanning tree (or forest) using Prim's algorithm with a priority queue.
        /// Node equality is reference-based; supply distinct Node instances per logical node.
        /// </summary>
        public static List<Edge> ComputeMST(
            List<Node> nodes,
            Func<Node, Node, double>? weightFunc = null)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (nodes.Count < 2)
                return new List<Edge>();

            weightFunc ??= (a, b) => a.DistanceTo(b);

            var mstEdges = new List<Edge>(nodes.Count - 1);
            var inMST = new HashSet<Node>(ReferenceEqualityComparer.Instance);
            var pq = new PriorityQueue<Edge, double>();

            // Seed from the first node; repeat from any disconnected node so we produce a forest.
            for (int seedIndex = 0; seedIndex < nodes.Count; seedIndex++)
            {
                var seed = nodes[seedIndex];
                if (inMST.Contains(seed))
                    continue;

                inMST.Add(seed);
                EnqueueEdgesFrom(seed, nodes, inMST, weightFunc, pq);

                while (pq.Count > 0)
                {
                    var edge = pq.Dequeue();
                    if (inMST.Contains(edge.To))
                        continue;

                    mstEdges.Add(edge);
                    inMST.Add(edge.To);
                    EnqueueEdgesFrom(edge.To, nodes, inMST, weightFunc, pq);
                }

                // After the queue drains, any remaining nodes are in a different component.
                // The outer loop will pick them up and start a new tree.
                pq.Clear();
            }

            return mstEdges;
        }

        private static void EnqueueEdgesFrom(
            Node from,
            List<Node> nodes,
            HashSet<Node> inMST,
            Func<Node, Node, double> weightFunc,
            PriorityQueue<Edge, double> pq)
        {
            foreach (var node in nodes)
            {
                if (inMST.Contains(node))
                    continue;
                var w = weightFunc(from, node);
                if (double.IsNaN(w) || double.IsInfinity(w))
                    continue;
                pq.Enqueue(new Edge(from, node, w), w);
            }
        }

        /// <summary>
        /// Computes the MST and adds extra non-MST edges for additional connectivity.
        /// </summary>
        public static List<Edge> ComputeMSTWithExtraEdges(
            List<Node> nodes,
            double extraEdgePercentage,
            Random random,
            Func<Node, Node, double>? weightFunc = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var mstEdges = ComputeMST(nodes, weightFunc);

            if (extraEdgePercentage <= 0 || mstEdges.Count == 0)
                return mstEdges;

            weightFunc ??= (a, b) => a.DistanceTo(b);

            // Track MST adjacency by reference identity.
            var mstAdjacency = new Dictionary<Node, HashSet<Node>>(ReferenceEqualityComparer.Instance);
            foreach (var edge in mstEdges)
            {
                GetOrAddSet(mstAdjacency, edge.From).Add(edge.To);
                GetOrAddSet(mstAdjacency, edge.To).Add(edge.From);
            }

            var possibleEdges = new List<Edge>();
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];
                    if (mstAdjacency.TryGetValue(a, out var neighbors) && neighbors.Contains(b))
                        continue;
                    var w = weightFunc(a, b);
                    if (double.IsNaN(w) || double.IsInfinity(w))
                        continue;
                    possibleEdges.Add(new Edge(a, b, w));
                }
            }

            int extraEdgeCount = (int)(mstEdges.Count * extraEdgePercentage);
            for (int i = 0; i < extraEdgeCount && possibleEdges.Count > 0; i++)
            {
                int index = random.Next(possibleEdges.Count);
                mstEdges.Add(possibleEdges[index]);
                possibleEdges.RemoveAt(index);
            }

            return mstEdges;
        }

        private static HashSet<Node> GetOrAddSet(Dictionary<Node, HashSet<Node>> dict, Node key)
        {
            if (!dict.TryGetValue(key, out var set))
            {
                set = new HashSet<Node>(ReferenceEqualityComparer.Instance);
                dict[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Rasterizes an edge to a list of grid cells using Bresenham's line algorithm.
        /// </summary>
        public static List<(int x, int y)> EdgesToPath(Edge edge)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));

            var path = new List<(int, int)>();

            int x0 = edge.From.X, y0 = edge.From.Y;
            int x1 = edge.To.X, y1 = edge.To.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                path.Add((x0, y0));

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return path;
        }
    }
}
