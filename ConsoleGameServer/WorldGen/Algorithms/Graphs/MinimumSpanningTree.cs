using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleGame.WorldGen.Algorithms.Graphs
{
    /// <summary>
    /// Minimum Spanning Tree implementation using Prim's algorithm.
    /// Used for connecting city districts, outdoor features, etc.
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
        /// Computes minimum spanning tree using Prim's algorithm.
        /// </summary>
        /// <param name="nodes">List of nodes to connect</param>
        /// <param name="weightFunc">Optional custom weight function (defaults to Euclidean distance)</param>
        /// <returns>List of edges forming the MST</returns>
        public static List<Edge> ComputeMST(
            List<Node> nodes,
            Func<Node, Node, double>? weightFunc = null)
        {
            if (nodes.Count < 2)
                return new List<Edge>();

            weightFunc ??= (a, b) => a.DistanceTo(b);

            var mstEdges = new List<Edge>();
            var inMST = new HashSet<Node> { nodes[0] };
            var candidates = new List<Edge>();

            // Add all edges from first node
            for (int i = 1; i < nodes.Count; i++)
            {
                candidates.Add(new Edge(nodes[0], nodes[i], weightFunc(nodes[0], nodes[i])));
            }

            while (inMST.Count < nodes.Count && candidates.Count > 0)
            {
                // Find minimum weight edge
                candidates.Sort((a, b) => a.Weight.CompareTo(b.Weight));
                var minEdge = candidates[0];
                candidates.RemoveAt(0);

                // Skip if both nodes already in MST
                if (inMST.Contains(minEdge.To))
                    continue;

                // Add edge to MST
                mstEdges.Add(minEdge);
                inMST.Add(minEdge.To);

                // Add new candidate edges from the newly added node
                foreach (var node in nodes)
                {
                    if (!inMST.Contains(node))
                    {
                        candidates.Add(new Edge(minEdge.To, node, weightFunc(minEdge.To, node)));
                    }
                }
            }

            return mstEdges;
        }

        /// <summary>
        /// Computes MST with additional random edges for more connectivity.
        /// </summary>
        /// <param name="nodes">List of nodes</param>
        /// <param name="extraEdgePercentage">Percentage of extra edges to add (0.0-1.0)</param>
        /// <param name="random">Random number generator</param>
        /// <returns>List of edges (MST + extra edges)</returns>
        public static List<Edge> ComputeMSTWithExtraEdges(
            List<Node> nodes,
            double extraEdgePercentage,
            Random random,
            Func<Node, Node, double>? weightFunc = null)
        {
            var mstEdges = ComputeMST(nodes, weightFunc);
            
            if (extraEdgePercentage <= 0)
                return mstEdges;

            weightFunc ??= (a, b) => a.DistanceTo(b);

            // Find all possible edges not in MST
            var mstEdgeSet = new HashSet<(Node, Node)>();
            foreach (var edge in mstEdges)
            {
                mstEdgeSet.Add((edge.From, edge.To));
                mstEdgeSet.Add((edge.To, edge.From));
            }

            var possibleEdges = new List<Edge>();
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (!mstEdgeSet.Contains((nodes[i], nodes[j])))
                    {
                        possibleEdges.Add(new Edge(nodes[i], nodes[j], weightFunc(nodes[i], nodes[j])));
                    }
                }
            }

            // Add random extra edges
            int extraEdgeCount = (int)(mstEdges.Count * extraEdgePercentage);
            for (int i = 0; i < extraEdgeCount && possibleEdges.Count > 0; i++)
            {
                int index = random.Next(possibleEdges.Count);
                mstEdges.Add(possibleEdges[index]);
                possibleEdges.RemoveAt(index);
            }

            return mstEdges;
        }

        /// <summary>
        /// Converts edges to a path (for road generation).
        /// </summary>
        public static List<(int x, int y)> EdgesToPath(Edge edge)
        {
            var path = new List<(int, int)>();
            
            // Bresenham's line algorithm
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

