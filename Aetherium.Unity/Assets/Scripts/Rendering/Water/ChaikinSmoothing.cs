#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Chaikin corner-cutting subdivision of a closed loop: each iteration replaces
    /// every vertex with two points at 1/4 and 3/4 of each edge, relaxing blocky
    /// corners into a smooth curve while staying inside the original outline
    /// (each new point is a convex blend of two originals). 0 iterations = blocky;
    /// 2-3 = organic coastline. Pure and scene-free.
    /// </summary>
    public static class ChaikinSmoothing
    {
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> closedLoop, int iterations)
        {
            var pts = new List<Vector2>(closedLoop);
            if (pts.Count < 3 || iterations <= 0)
                return pts;

            for (int k = 0; k < iterations; k++)
            {
                var next = new List<Vector2>(pts.Count * 2);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[(i + 1) % pts.Count];
                    next.Add(a * 0.75f + b * 0.25f);
                    next.Add(a * 0.25f + b * 0.75f);
                }
                pts = next;
            }

            return pts;
        }

        /// <summary>Converts an integer corner loop to floats and smooths it.</summary>
        public static List<Vector2> Smooth(IReadOnlyList<Vector2Int> closedLoop, int iterations)
        {
            var floats = new List<Vector2>(closedLoop.Count);
            foreach (var c in closedLoop)
                floats.Add(new Vector2(c.x, c.y));

            return Smooth(floats, iterations);
        }
    }
}
