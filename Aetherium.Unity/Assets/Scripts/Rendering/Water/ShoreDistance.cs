#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Distance from a point to the nearest coastline (region boundary loop). Baked
    /// per-vertex into the water mesh so the shader can draw a foam/shallows band that
    /// hugs the shore: 0 at the coastline, 1 in open water past a configurable shore
    /// width. Pure and scene-free.
    /// </summary>
    public static class ShoreDistance
    {
        /// <summary>Shortest distance from <paramref name="p"/> to any loop's edges.</summary>
        public static float ToLoops(Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> loops)
        {
            float best = float.PositiveInfinity;
            for (int li = 0; li < loops.Count; li++)
            {
                IReadOnlyList<Vector2> loop = loops[li];
                int n = loop.Count;
                if (n < 2)
                    continue;

                for (int i = 0; i < n; i++)
                {
                    Vector2 a = loop[i];
                    Vector2 b = loop[(i + 1) % n];
                    float d = DistanceToSegment(p, a, b);
                    if (d < best)
                        best = d;
                }
            }

            return float.IsPositiveInfinity(best) ? 0f : best;
        }

        /// <summary>Distance from <paramref name="p"/> to segment [<paramref name="a"/>, <paramref name="b"/>].</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 1e-12f)
                return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(p, projection);
        }

        /// <summary>Normalizes a raw distance to 0..1 over <paramref name="shoreWidth"/>.</summary>
        public static float Normalized(float distance, float shoreWidth)
        {
            if (shoreWidth <= 0f)
                return 1f;

            return Mathf.Clamp01(distance / shoreWidth);
        }
    }
}
