#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Pure adaptive-framing math: how the camera and mode escalate to the local vertical extent. More occupied
    /// bands in view → a larger orthographic size (pull back), and past a threshold → surface the cross-section
    /// overlay. Holds no scene state, so it is unit tested directly in EditMode.
    /// </summary>
    public static class DepthFraming
    {
        /// <summary>Occupied-band count at/above which the cross-section overlay is surfaced.</summary>
        public const int DefaultEscalationThreshold = 4;

        public const float DefaultBaseSize = 5f;
        public const float DefaultPerBand = 1.5f;
        public const float DefaultMaxSize = 14f;

        /// <summary>Distinct altitude bands present in the frame, including the player's own band.</summary>
        public static int OccupiedBandCount(PerceptionLite perception)
        {
            if (perception == null)
                return 0;

            var bands = new HashSet<int>();
            foreach (var visual in perception.Visuals.Values)
                bands.Add(visual.Location.Z);
            if (perception.PlayerLocation != null)
                bands.Add(perception.PlayerLocation.Z);

            return bands.Count;
        }

        /// <summary>
        /// Orthographic half-height for a frame with <paramref name="bandCount"/> occupied bands: a flat frame
        /// (≤1 band) stays at <paramref name="baseSize"/> and each extra band pulls the camera back by
        /// <paramref name="perBand"/>, clamped to <paramref name="maxSize"/>.
        /// </summary>
        public static float OrthographicSizeFor(
            int bandCount,
            float baseSize = DefaultBaseSize,
            float perBand = DefaultPerBand,
            float maxSize = DefaultMaxSize)
        {
            int extra = Mathf.Max(0, bandCount - 1);
            return Mathf.Min(baseSize + perBand * extra, maxSize);
        }

        /// <summary>True when vertical complexity warrants surfacing the cross-section view.</summary>
        public static bool ShouldSurfaceCrossSection(int bandCount, int threshold = DefaultEscalationThreshold)
            => bandCount >= threshold;
    }
}
