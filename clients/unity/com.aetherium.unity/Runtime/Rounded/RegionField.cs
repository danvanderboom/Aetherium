#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rounded
{
    /// <summary>
    /// Inside/outside test and signed distance for a terrain region defined by its
    /// boundary loops (outer loops CCW, island holes CW, as produced by
    /// <see cref="MarchingSquares"/>). The winding number handles islands for free —
    /// a point in the region winds to +1, a point inside an island winds to 0. Signed
    /// distance is positive inside the coastline and negative outside: the field the
    /// region shader thresholds to draw a smooth curved edge. Pure and scene-free.
    /// </summary>
    public static class RegionField
    {
        /// <summary>True when <paramref name="p"/> is inside the region (not on an island).</summary>
        public static bool Inside(Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> loops)
        {
            int winding = 0;
            for (int li = 0; li < loops.Count; li++)
            {
                IReadOnlyList<Vector2> loop = loops[li];
                int n = loop.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = loop[i];
                    Vector2 b = loop[(i + 1) % n];
                    if (a.y <= p.y)
                    {
                        if (b.y > p.y && IsLeft(a, b, p) > 0f)
                            winding++;
                    }
                    else
                    {
                        if (b.y <= p.y && IsLeft(a, b, p) < 0f)
                            winding--;
                    }
                }
            }
            return winding != 0;
        }

        /// <summary>
        /// Signed distance to the nearest coastline: positive inside the region,
        /// negative outside. Magnitude is the unsigned distance to the boundary.
        /// </summary>
        public static float SignedDistance(Vector2 p, IReadOnlyList<IReadOnlyList<Vector2>> loops)
        {
            float d = ShoreDistance.ToLoops(p, loops);
            return Inside(p, loops) ? d : -d;
        }

        // > 0 when p is left of the directed edge a->b (used for the winding test).
        private static float IsLeft(Vector2 a, Vector2 b, Vector2 p)
            => (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);
    }
}
