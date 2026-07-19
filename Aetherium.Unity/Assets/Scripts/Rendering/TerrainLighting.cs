#nullable enable
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Folds per-cell light level and a scene ambient tint into a base terrain color.
    /// This extends the renderer's existing tint model instead of adding real-time
    /// lights, so the 3D URP pipeline (Deferred + SSAO) and the band-stack alpha
    /// compositing stay untouched. Pure and scene-free.
    /// </summary>
    public static class TerrainLighting
    {
        /// <summary>Neutral ambient tint (leaves colors unchanged).</summary>
        public static readonly Color NeutralTint = Color.white;

        /// <summary>
        /// Darkens <paramref name="baseColor"/> by <paramref name="lightLevel"/> (clamped
        /// to 0..1) and tints it toward <paramref name="ambientTint"/> (white = no change).
        /// Alpha is preserved.
        /// </summary>
        public static Color Modulate(Color baseColor, double lightLevel, Color ambientTint)
        {
            float l = Mathf.Clamp01((float)lightLevel);
            return new Color(
                baseColor.r * l * ambientTint.r,
                baseColor.g * l * ambientTint.g,
                baseColor.b * l * ambientTint.b,
                baseColor.a);
        }
    }
}
