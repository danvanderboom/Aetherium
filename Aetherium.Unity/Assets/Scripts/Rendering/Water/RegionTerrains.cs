#nullable enable
using System;
using System.Collections.Generic;
using Aetherium.Unity.Model;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Single source of truth for which terrains are rendered as smooth "region"
    /// shapes (a generated water/lava mesh) instead of blocky Tilemap squares. Shared
    /// by <see cref="TerrainRegionMask"/> (what the mesh covers) and the tile renderer
    /// (which cells to skip) so the two never double-draw. Pure and scene-free.
    /// </summary>
    public static class RegionTerrains
    {
        // Default terrain names that get the rounded-mesh treatment.
        private static readonly HashSet<string> Default =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "water", "lava" };

        /// <summary>True when <paramref name="terrainName"/> is a region terrain.</summary>
        public static bool IsRegion(string? terrainName)
        {
            return !string.IsNullOrEmpty(terrainName) && Default.Contains(terrainName!.Trim());
        }

        /// <summary>
        /// Resolves the display name used for theming / region matching, mirroring
        /// <c>TilemapRenderer2D.GetOrCreateTile</c>: prefer the <c>TileTypes</c> entry's
        /// Name, else the raw id.
        /// </summary>
        public static string ResolveName(PerceptionLite? perception, string? tileTypeId)
        {
            if (perception?.TileTypes != null && !string.IsNullOrEmpty(tileTypeId) &&
                perception.TileTypes.TryGetValue(tileTypeId!, out var type) &&
                !string.IsNullOrEmpty(type.Name))
            {
                return type.Name;
            }

            return tileTypeId ?? string.Empty;
        }

        /// <summary>True when the visual's resolved terrain is a region terrain.</summary>
        public static bool IsRegionVisual(PerceptionLite? perception, VisualLite? visual)
        {
            if (visual == null)
                return false;

            return IsRegion(ResolveName(perception, visual.TileTypeId));
        }
    }
}
