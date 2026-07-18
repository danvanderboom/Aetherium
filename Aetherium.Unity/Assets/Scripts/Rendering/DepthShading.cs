#nullable enable
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Pure depth-cue math shared by the Unity band stack. Mirrors the console
    /// composite's <c>DepthFactor = 1 / (1 + k·|dZ|)</c> so the two renderers agree
    /// on how fast off-focus bands fall off. Holds no scene state, so it is unit
    /// tested directly in EditMode.
    /// </summary>
    public static class DepthShading
    {
        /// <summary>Falloff constant k. 0.5 matches the console's DepthFactor ramp.</summary>
        public const float DefaultFalloff = 0.5f;

        /// <summary>Deep bands never fade below this so they stay faintly legible.</summary>
        public const float DefaultMinAlpha = 0.15f;

        /// <summary>
        /// Opacity for a band <paramref name="dZ"/> away from the focus band. The
        /// focus band (dZ == 0) is fully opaque (1.0); deeper bands fall off as
        /// <c>1 / (1 + falloff·|dZ|)</c>, clamped to <paramref name="minAlpha"/>.
        /// Occluded off-focus cells are dropped server-side, so this only cues the
        /// cells that genuinely pass the 3D FOV test.
        /// </summary>
        public static float AlphaForDepth(int dZ, float falloff = DefaultFalloff, float minAlpha = DefaultMinAlpha)
        {
            int depth = Mathf.Abs(dZ);
            if (depth == 0)
                return 1f;

            float alpha = 1f / (1f + falloff * depth);
            return Mathf.Max(alpha, minAlpha);
        }

        /// <summary>
        /// Sorting order for a band's <c>TilemapRenderer</c>. Higher altitude draws
        /// above lower altitude (physically nearer an overhead camera), so a bird at
        /// +2 composites over the street at 0, which composites over a subway at −1.
        /// </summary>
        public static int SortingOrderForBand(int bandZ) => bandZ;
    }
}
