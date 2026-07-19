#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Traces the boundary of a set of unit grid cells into closed loops of integer
    /// corner points (the marching-squares outline of the cell union). Outer
    /// boundaries and holes (islands) come back as separate loops with opposite
    /// winding; collinear points are dropped so each loop is just its turn corners.
    /// Pure and scene-free — the smoothing/mesh stages consume its output.
    /// </summary>
    public static class MarchingSquares
    {
        private readonly struct Corner : System.IEquatable<Corner>
        {
            public readonly int X;
            public readonly int Y;
            public Corner(int x, int y) { X = x; Y = y; }
            public bool Equals(Corner o) => X == o.X && Y == o.Y;
            public override bool Equals(object? o) => o is Corner c && Equals(c);
            public override int GetHashCode() => (X * 397) ^ Y;
        }

        /// <summary>Traces boundary loops for the given occupied cells.</summary>
        public static List<List<Vector2Int>> TraceLoops(IEnumerable<(int x, int y)> cells)
        {
            var set = new HashSet<(int, int)>(cells);

            // Every cell contributes its 4 edges wound consistently (clockwise in grid
            // space). A shared internal edge appears as A->B from one cell and B->A from
            // its neighbour and cancels, leaving only the outline of the union.
            var edges = new HashSet<(Corner a, Corner b)>();
            foreach (var (x, y) in set)
            {
                AddOrCancel(edges, new Corner(x, y), new Corner(x + 1, y));         // top
                AddOrCancel(edges, new Corner(x + 1, y), new Corner(x + 1, y + 1)); // right
                AddOrCancel(edges, new Corner(x + 1, y + 1), new Corner(x, y + 1)); // bottom
                AddOrCancel(edges, new Corner(x, y + 1), new Corner(x, y));         // left
            }

            // Adjacency for linking: start corner -> outgoing end corners. Out-degree is
            // usually 1; it exceeds 1 only at pinch points (diagonal cell touches).
            var outgoing = new Dictionary<Corner, List<Corner>>();
            foreach (var (a, b) in edges)
            {
                if (!outgoing.TryGetValue(a, out var list))
                {
                    list = new List<Corner>();
                    outgoing[a] = list;
                }
                list.Add(b);
            }

            var loops = new List<List<Vector2Int>>();
            foreach (var start in outgoing.Keys.ToList())
            {
                // A pinch-point corner can seed more than one loop.
                while (HasOutgoing(outgoing, start))
                {
                    var loop = TraceFrom(outgoing, start);
                    if (loop.Count >= 3)
                        loops.Add(Simplify(loop));
                }
            }

            return loops;
        }

        private static List<Corner> TraceFrom(Dictionary<Corner, List<Corner>> outgoing, Corner first)
        {
            var loop = new List<Corner> { first };
            var cur = first;
            while (TryPop(outgoing, cur, out var next))
            {
                if (next.Equals(first))
                    break; // closed the loop
                loop.Add(next);
                cur = next;
            }
            return loop;
        }

        private static void AddOrCancel(HashSet<(Corner, Corner)> edges, Corner a, Corner b)
        {
            if (edges.Contains((b, a)))
                edges.Remove((b, a));
            else
                edges.Add((a, b));
        }

        private static bool HasOutgoing(Dictionary<Corner, List<Corner>> outgoing, Corner c)
            => outgoing.TryGetValue(c, out var list) && list.Count > 0;

        private static bool TryPop(Dictionary<Corner, List<Corner>> outgoing, Corner c, out Corner next)
        {
            if (outgoing.TryGetValue(c, out var list) && list.Count > 0)
            {
                next = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                return true;
            }
            next = default;
            return false;
        }

        private static List<Vector2Int> Simplify(List<Corner> loop)
        {
            int n = loop.Count;
            var result = new List<Vector2Int>(n);
            for (int i = 0; i < n; i++)
            {
                Corner p0 = loop[(i - 1 + n) % n];
                Corner p1 = loop[i];
                Corner p2 = loop[(i + 1) % n];
                long cross = (long)(p1.X - p0.X) * (p2.Y - p1.Y) - (long)(p1.Y - p0.Y) * (p2.X - p1.X);
                if (cross != 0)
                    result.Add(new Vector2Int(p1.X, p1.Y));
            }

            // A closed cell region always has turns; this guards pathological input.
            if (result.Count < 3)
            {
                result.Clear();
                foreach (var c in loop)
                    result.Add(new Vector2Int(c.X, c.Y));
            }

            return result;
        }
    }
}
