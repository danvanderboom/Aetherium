#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Deterministic tile-type → color mapping so bands and terrains are visually
    /// distinguishable. Known terrain names get a hand-picked palette; anything
    /// else hashes to a stable, saturated hue (same id → same color every run, so
    /// unknown types never collapse into the gray fallback). Pure and
    /// side-effect-free — unit tested without a scene.
    /// </summary>
    public static class TileTheme
    {
        private static readonly Color Fallback = new Color(0.5f, 0.5f, 0.5f);

        // Hand-picked, reasonably spread palette for common terrains/structures.
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            ["stone"] = new Color(0.60f, 0.60f, 0.62f),
            ["rock"]  = new Color(0.50f, 0.48f, 0.45f),
            ["dirt"]  = new Color(0.55f, 0.40f, 0.25f),
            ["grass"] = new Color(0.35f, 0.62f, 0.30f),
            ["sand"]  = new Color(0.85f, 0.78f, 0.50f),
            ["water"] = new Color(0.20f, 0.45f, 0.80f),
            ["lava"]  = new Color(0.85f, 0.30f, 0.10f),
            ["wood"]  = new Color(0.55f, 0.36f, 0.20f),
            ["metal"] = new Color(0.65f, 0.68f, 0.72f),
            ["snow"]  = new Color(0.92f, 0.94f, 0.97f),
            ["wall"]  = new Color(0.42f, 0.42f, 0.45f),
            ["floor"] = new Color(0.70f, 0.68f, 0.64f),
            ["road"]  = new Color(0.30f, 0.30f, 0.32f),
            ["track"] = new Color(0.45f, 0.42f, 0.38f),
        };

        /// <summary>
        /// Stable color for a tile type id/name. Matching is case-insensitive;
        /// unknown names hash to a stable hue. Null/empty returns the gray fallback.
        /// </summary>
        public static Color ColorFor(string? tileTypeId)
        {
            if (string.IsNullOrEmpty(tileTypeId))
                return Fallback;

            string key = tileTypeId!.Trim().ToLowerInvariant();
            if (Palette.TryGetValue(key, out var color))
                return color;

            return HashedColor(key);
        }

        /// <summary>
        /// FNV-1a hash of the id mapped to a saturated hue, so unknown tile types
        /// still get distinct, deterministic colors.
        /// </summary>
        public static Color HashedColor(string key)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char ch in key)
                {
                    hash ^= ch;
                    hash *= 16777619u;
                }

                float hue = (hash % 360u) / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
            }
        }
    }
}
