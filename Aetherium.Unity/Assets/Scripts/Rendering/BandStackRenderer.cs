#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Renders a multi-band perception slab as a stack of per-band tilemaps — the
    /// Unity-native equivalent of the console depth composite. Each altitude band in
    /// the slab gets its own child <see cref="Tilemap"/> drawn by a
    /// <see cref="TilemapRenderer2D"/>, sorted by altitude and faded by |dZ| from the
    /// focus band (<see cref="DepthShading"/>). The focus band renders opaque; deeper
    /// bands ghost out so you can see down a stairwell or up at an overpass.
    ///
    /// Bands are created lazily and reused across frames; a band that empties out is
    /// cleared (not destroyed) so the stack stays stable as the player moves.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class BandStackRenderer : MonoBehaviour
    {
        [SerializeField] private TileBase? defaultTile;
        [SerializeField] private float depthFalloff = DepthShading.DefaultFalloff;
        [SerializeField] private float minAlpha = DepthShading.DefaultMinAlpha;
        [SerializeField] private bool skipRegionTerrain = false;
        [SerializeField] private bool applyLighting = false;

        private readonly Dictionary<int, TilemapRenderer2D> bands = new Dictionary<int, TilemapRenderer2D>();
        private readonly HashSet<int> bandsThisFrame = new HashSet<int>();
        private int focusZ;

        /// <summary>The band currently drawn at full opacity (the player's Z).</summary>
        public int FocusZ => focusZ;

        /// <summary>
        /// Propagated to every band's tilemap: when true, region terrain (water/lava)
        /// is not drawn as tiles, leaving it to a companion
        /// <see cref="Water.WaterRegionRenderer"/> that draws it as a smooth mesh.
        /// </summary>
        public bool SkipRegionTerrain
        {
            get => skipRegionTerrain;
            set
            {
                skipRegionTerrain = value;
                foreach (var band in bands.Values)
                    band.SkipRegionTerrain = value;
            }
        }

        /// <summary>
        /// Propagated to every band's tilemap: when true, each cell is shaded by its
        /// light level and the frame's ambient tint (<see cref="TerrainLighting"/>).
        /// </summary>
        public bool ApplyLighting
        {
            get => applyLighting;
            set
            {
                applyLighting = value;
                foreach (var band in bands.Values)
                    band.ApplyLighting = value;
            }
        }

        /// <summary>
        /// Renders the slab using the player's own band as the focus band. This is
        /// the auto-follow behaviour (Section 5.x): focus tracks the player's Z.
        /// </summary>
        public void RenderPerception(PerceptionLite perception)
        {
            int focus = perception?.PlayerLocation?.Z ?? 0;
            RenderPerception(perception, focus);
        }

        /// <summary>Renders the slab with an explicit focus band.</summary>
        public void RenderPerception(PerceptionLite perception, int focusZ)
        {
            if (perception == null)
                return;

            this.focusZ = focusZ;

            // Bands to touch this frame = focus ∪ bands with cells now ∪ bands that
            // had a tilemap last frame (so an emptied band gets a final clearing pass).
            bandsThisFrame.Clear();
            bandsThisFrame.Add(focusZ);
            foreach (var visual in perception.Visuals.Values)
                bandsThisFrame.Add(visual.Location.Z);
            foreach (var existing in bands.Keys)
                bandsThisFrame.Add(existing);

            foreach (var z in bandsThisFrame)
            {
                var band = GetOrCreateBand(z);
                band.RenderPerception(perception, z);

                int dz = Mathf.Abs(z - focusZ);
                band.SetLayerAlpha(DepthShading.AlphaForDepth(dz, depthFalloff, minAlpha));
                band.SetSortingOrder(DepthShading.SortingOrderForBand(z));
            }
        }

        private TilemapRenderer2D GetOrCreateBand(int z)
        {
            if (bands.TryGetValue(z, out var existing))
                return existing;

            var go = new GameObject($"Band_{z}");
            go.transform.SetParent(transform, false);

            // Parent already carries the Grid (RequireComponent), so these tilemaps
            // share one cell layout.
            var bandTilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            var renderer = go.AddComponent<TilemapRenderer2D>();
            renderer.Configure(bandTilemap, defaultTile);
            renderer.SkipRegionTerrain = skipRegionTerrain;
            renderer.ApplyLighting = applyLighting;

            bands[z] = renderer;
            return renderer;
        }

        // --- inspection / test surface ---

        /// <summary>Total bands ever created (including cleared, still-tracked ones).</summary>
        public int TrackedBandCount => bands.Count;

        /// <summary>Bands that currently have at least one tile drawn.</summary>
        public int ActiveBandCount
        {
            get
            {
                int count = 0;
                foreach (var band in bands.Values)
                {
                    if (band.GetRenderedTileCount() > 0)
                        count++;
                }
                return count;
            }
        }

        /// <summary>The renderer for band <paramref name="z"/>, or null if untracked.</summary>
        public TilemapRenderer2D? GetBand(int z) => bands.TryGetValue(z, out var band) ? band : null;

        /// <summary>Current opacity of band <paramref name="z"/>; false if untracked.</summary>
        public bool TryGetBandAlpha(int z, out float alpha)
        {
            if (bands.TryGetValue(z, out var band))
            {
                alpha = band.GetLayerAlpha();
                return true;
            }

            alpha = 0f;
            return false;
        }
    }
}
