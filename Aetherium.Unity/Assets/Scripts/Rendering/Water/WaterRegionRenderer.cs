#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using UnityEngine;

namespace Aetherium.Unity.Rendering.Water
{
    /// <summary>
    /// Renders region terrain (water/lava) as a smooth mesh per altitude band, slotting
    /// into the same depth compositing as the band-stack tilemaps: each band's water
    /// fades by |dZ| from focus (through the shader's <c>_BandAlpha</c>) and sorts by
    /// altitude, sitting just behind that band's land tiles so islands read correctly.
    /// A band's mesh rebuilds only when its water cell-set changes. Pair with a
    /// <see cref="Aetherium.Unity.Rendering.TilemapRenderer2D"/> whose
    /// <c>SkipRegionTerrain</c> is on so land and water never double-draw.
    /// </summary>
    public class WaterRegionRenderer : MonoBehaviour
    {
        [SerializeField] private Material? waterMaterial;
        [SerializeField] private int smoothIterations = 2;
        [SerializeField] private int subdivisions = 3;
        [SerializeField] private float shoreWidth = 1.0f;
        [SerializeField] private float cellSize = 1.0f;
        [SerializeField] private float zOffset = 0.5f;
        [SerializeField] private float depthFalloff = DepthShading.DefaultFalloff;
        [SerializeField] private float minAlpha = DepthShading.DefaultMinAlpha;

        private sealed class Band
        {
            public GameObject Go = null!;
            public MeshFilter Filter = null!;
            public MeshRenderer Renderer = null!;
            public Mesh Mesh = null!;
            public int CellHash;
            public float BandAlpha;
        }

        private readonly Dictionary<int, Band> bands = new Dictionary<int, Band>();
        private readonly HashSet<int> bandsThisFrame = new HashSet<int>();
        private MaterialPropertyBlock? propertyBlock;

        /// <summary>Renders region terrain using the player's own band as focus.</summary>
        public void RenderPerception(PerceptionLite perception)
            => RenderPerception(perception, perception?.PlayerLocation?.Z ?? 0);

        /// <summary>Renders region terrain with an explicit focus band.</summary>
        public void RenderPerception(PerceptionLite? perception, int focusZ)
        {
            if (perception == null)
                return;

            // Bands with water this frame ∪ bands we drew last frame (so an emptied
            // band gets a final clearing pass).
            bandsThisFrame.Clear();
            foreach (var visual in perception.Visuals.Values)
            {
                if (RegionTerrains.IsRegionVisual(perception, visual))
                    bandsThisFrame.Add(visual.Location.Z);
            }
            foreach (var z in bands.Keys)
                bandsThisFrame.Add(z);

            Color ambient = perception.AmbientTintColor;
            foreach (int z in bandsThisFrame)
            {
                var mask = TerrainRegionMask.Build(perception, z);
                UpdateBand(z, mask, Mathf.Abs(z - focusZ), ambient);
            }
        }

        private void UpdateBand(int z, TerrainRegionMask mask, int dz, Color ambientTint)
        {
            Band band = GetOrCreateBand(z);

            int hash = CellSetHash(mask);
            if (hash != band.CellHash)
            {
                var data = WaterMeshBuilder.Build(
                    mask.Cells, smoothIterations, subdivisions, shoreWidth, cellSize, zOffset);
                WaterMeshBuilder.Fill(band.Mesh, data);
                band.CellHash = hash;
            }

            float alpha = DepthShading.AlphaForDepth(dz, depthFalloff, minAlpha);
            band.BandAlpha = alpha;
            band.Renderer.sortingOrder = DepthShading.SortingOrderForBand(z);

            propertyBlock ??= new MaterialPropertyBlock();
            band.Renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_BandAlpha", alpha);
            propertyBlock.SetColor("_AmbientTint", ambientTint);
            // Fallback for a placeholder Unlit material: also carry tint + alpha via _BaseColor.
            propertyBlock.SetColor("_BaseColor", new Color(ambientTint.r, ambientTint.g, ambientTint.b, alpha));
            band.Renderer.SetPropertyBlock(propertyBlock);
        }

        private Band GetOrCreateBand(int z)
        {
            if (bands.TryGetValue(z, out var existing))
                return existing;

            var go = new GameObject($"Water_{z}");
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = $"Water_{z}" };
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = waterMaterial != null ? waterMaterial : GetFallbackMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var band = new Band
            {
                Go = go,
                Filter = filter,
                Renderer = renderer,
                Mesh = mesh,
                CellHash = -1,
            };
            bands[z] = band;
            return band;
        }

        private static Material? fallbackMaterial;

        private static Material? GetFallbackMaterial()
        {
            if (fallbackMaterial != null)
                return fallbackMaterial;

            Shader? shader = Shader.Find("Aetherium/RoundedWater");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            fallbackMaterial = new Material(shader) { name = "WaterFallback" };
            return fallbackMaterial;
        }

        // Order-independent hash of a band's water cell-set (drives the dirty check).
        private static int CellSetHash(TerrainRegionMask mask)
        {
            int hash = 17 + mask.Count * 92821;
            foreach (var (x, y) in mask.Cells)
                hash ^= (x * 73856093) ^ (y * 19349663);
            return hash;
        }

        // --- inspection / test surface ---

        /// <summary>Number of bands with a water object (including emptied, still-tracked ones).</summary>
        public int BandObjectCount => bands.Count;

        /// <summary>The mesh for band <paramref name="z"/>, or null if untracked.</summary>
        public Mesh? GetBandMesh(int z) => bands.TryGetValue(z, out var band) ? band.Mesh : null;

        /// <summary>The depth alpha applied to band <paramref name="z"/>; false if untracked.</summary>
        public bool TryGetBandAlpha(int z, out float alpha)
        {
            if (bands.TryGetValue(z, out var band))
            {
                alpha = band.BandAlpha;
                return true;
            }
            alpha = 0f;
            return false;
        }

        /// <summary>The sorting order applied to band <paramref name="z"/>; false if untracked.</summary>
        public bool TryGetBandSortingOrder(int z, out int order)
        {
            if (bands.TryGetValue(z, out var band))
            {
                order = band.Renderer.sortingOrder;
                return true;
            }
            order = 0;
            return false;
        }
    }
}
