#nullable enable
using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Spatial;
using Aetherium.Unity.Rendering.Water;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Renders PerceptionLite data to a Unity 2D Tilemap with Z-level support.
    /// </summary>
    public class TilemapRenderer2D : MonoBehaviour
    {
        [SerializeField] private Tilemap? tilemap;
        [SerializeField] private TileBase? defaultTile;
        [SerializeField] private bool skipRegionTerrain = false;
        [SerializeField] private bool applyLighting = false;
        private Color ambientTint = Color.white;
        private TilemapRenderer? tilemapRenderer;
        private Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();
        private TileBase? fallbackTile;

        // A single 1×1 white sprite shared by every runtime-generated Tile; each
        // Tile just tints it via Tile.color (see TileTheme), so we never allocate a
        // texture per tile type. Static so the whole band stack shares one sprite.
        private static Sprite? sharedSprite;
        private static Texture2D? sharedTexture;

        private int currentZLevel = 0;
        private PerceptionLite? currentPerception;

        // Cells written in the previous render pass. RenderPerception only clears
        // cells in (previous \ current) and only writes the (current) batch, so we
        // avoid ClearAllTiles + per-tile SetTile every frame.
        private readonly HashSet<Vector3Int> previousCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> currentCells = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> positionsScratch = new List<Vector3Int>();
        private readonly List<TileBase> tilesScratch = new List<TileBase>();
        private readonly List<Color> lightColorsScratch = new List<Color>();

        private void Awake()
        {
            if (tilemap == null)
            {
                tilemap = GetComponent<Tilemap>();
                if (tilemap == null)
                {
                    Debug.LogError("TilemapRenderer2D requires a Tilemap component.");
                }
            }

            if (tilemapRenderer == null)
                tilemapRenderer = GetComponent<TilemapRenderer>();
        }

        /// <summary>
        /// Injects the Tilemap and fallback tile when this renderer is created at
        /// runtime (e.g. by <see cref="BandStackRenderer"/>) after Awake has run.
        /// </summary>
        public void Configure(Tilemap targetTilemap, TileBase? fallback)
        {
            tilemap = targetTilemap;
            defaultTile = fallback;
            tilemapRenderer = GetComponent<TilemapRenderer>();
        }

        /// <summary>
        /// When true, cells whose terrain is a smooth "region" type (water/lava) are
        /// not drawn as tiles — a companion <see cref="Water.WaterRegionRenderer"/>
        /// draws them as a mesh instead, so the two never double-draw. Default false
        /// (unchanged behaviour).
        /// </summary>
        public bool SkipRegionTerrain
        {
            get => skipRegionTerrain;
            set => skipRegionTerrain = value;
        }

        /// <summary>
        /// When true, each cell's rendered color is multiplied by its per-cell light
        /// level and the frame's ambient tint (<see cref="TerrainLighting"/>). The tile's
        /// own base color is unchanged (so palette readback/tests still hold). Default
        /// false — and with light level 1.0 + a white tint it is a no-op anyway.
        /// </summary>
        public bool ApplyLighting
        {
            get => applyLighting;
            set => applyLighting = value;
        }

        /// <summary>
        /// Sets this band's overall opacity by tinting the whole tilemap. RGB stays
        /// white so each tile's own <see cref="TileTheme"/> color shows through; only
        /// alpha is scaled. Used by the band stack for depth falloff.
        /// </summary>
        public void SetLayerAlpha(float alpha)
        {
            if (tilemap == null)
                return;

            tilemap.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        /// <summary>Current layer alpha (tilemap tint alpha); 0 if no tilemap.</summary>
        public float GetLayerAlpha() => tilemap != null ? tilemap.color.a : 0f;

        /// <summary>Sets the draw order for this band (higher = drawn on top).</summary>
        public void SetSortingOrder(int order)
        {
            if (tilemapRenderer == null)
                tilemapRenderer = GetComponent<TilemapRenderer>();

            if (tilemapRenderer != null)
                tilemapRenderer.sortingOrder = order;
        }

        /// <summary>Current sorting order; 0 if no renderer.</summary>
        public int GetSortingOrder() => tilemapRenderer != null ? tilemapRenderer.sortingOrder : 0;

        /// <summary>
        /// Reads back the themed color of the tile at grid cell (<paramref name="gridX"/>,
        /// <paramref name="gridY"/>), mirroring the grid→cell mapping used when writing.
        /// Returns false when no tile is present. Intended for verification/tests.
        /// </summary>
        public bool TryGetTileColor(int gridX, int gridY, out Color color)
        {
            color = default;
            if (tilemap == null)
                return false;

            var worldPos = GridHelpers.GridToWorld(gridX, gridY);
            var cellPos = tilemap.WorldToCell(worldPos);
            if (tilemap.GetTile(cellPos) is Tile tile)
            {
                color = tile.color;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renders the current perception to the tilemap at the specified Z-level.
        /// </summary>
        public void RenderPerception(PerceptionLite perception, int zLevel)
        {
            currentPerception = perception;
            currentZLevel = zLevel;

            if (tilemap == null || perception == null)
                return;

            currentCells.Clear();
            positionsScratch.Clear();
            tilesScratch.Clear();
            lightColorsScratch.Clear();
            ambientTint = perception.AmbientTintColor;

            foreach (var visual in perception.Visuals.Values)
            {
                if (visual.Location.Z != zLevel)
                    continue;

                // A companion WaterRegionRenderer draws region terrain (water/lava) as a
                // smooth mesh; skip those cells here so the two never double-draw.
                if (skipRegionTerrain && RegionTerrains.IsRegionVisual(perception, visual))
                    continue;

                var worldPos = GridHelpers.GridToWorld(visual.Location);
                var cellPos = tilemap.WorldToCell(worldPos);
                if (!currentCells.Add(cellPos))
                    continue; // duplicate visual at the same cell, ignore

                positionsScratch.Add(cellPos);
                tilesScratch.Add(GetOrCreateTile(visual.TileTypeId, perception.TileTypes));
                // Per-cell lighting factor (multiplies the tile's base color). White when
                // lighting is off, so it is a no-op unless opted in.
                lightColorsScratch.Add(applyLighting
                    ? TerrainLighting.Modulate(Color.white, visual.LightLevel, ambientTint)
                    : Color.white);
            }

            // Clear cells that were set last frame but are no longer present.
            foreach (var prev in previousCells)
            {
                if (!currentCells.Contains(prev))
                    tilemap.SetTile(prev, null);
            }

            if (positionsScratch.Count > 0)
            {
                tilemap.SetTiles(positionsScratch.ToArray(), tilesScratch.ToArray());

                // Multiply per-cell lighting over each tile's base color. Band alpha
                // (the tilemap tint) still multiplies over all of it.
                if (applyLighting)
                {
                    for (int i = 0; i < positionsScratch.Count; i++)
                        tilemap.SetColor(positionsScratch[i], lightColorsScratch[i]);
                }
            }

            // Swap: previousCells becomes the set we just wrote.
            previousCells.Clear();
            foreach (var c in currentCells)
                previousCells.Add(c);
        }

        /// <summary>
        /// Sets the Z-level to render and updates the tilemap.
        /// </summary>
        public void SetZLevel(int zLevel)
        {
            if (currentPerception != null)
            {
                RenderPerception(currentPerception, zLevel);
            }
        }

        /// <summary>
        /// Gets the current Z-level being rendered.
        /// </summary>
        public int GetCurrentZLevel() => currentZLevel;

        /// <summary>
        /// Gets the number of tiles currently written to the tilemap.
        /// </summary>
        public int GetRenderedTileCount() => previousCells.Count;

        private TileBase GetOrCreateTile(string tileTypeId, Dictionary<string, TileTypeLite> tileTypes)
        {
            if (string.IsNullOrEmpty(tileTypeId))
            {
                return GetFallbackTile();
            }

            if (tileCache.TryGetValue(tileTypeId, out var cachedTile))
            {
                return cachedTile;
            }

            // Prefer the display name from the TileTypes table for palette matching,
            // falling back to the id itself when the type wasn't sent. Either way an
            // unknown name still hashes to a stable color rather than gray.
            string themeKey = tileTypeId;
            if (tileTypes != null && tileTypes.TryGetValue(tileTypeId, out var tileType) &&
                !string.IsNullOrEmpty(tileType.Name))
            {
                themeKey = tileType.Name;
            }

            var tile = CreateColoredTile(TileTheme.ColorFor(themeKey));
            tileCache[tileTypeId] = tile;
            return tile;
        }

        private TileBase GetFallbackTile()
        {
            if (defaultTile != null)
                return defaultTile;

            // Lazily create a single fallback Tile so we don't allocate per frame
            // when no inspector-assigned defaultTile is provided.
            if (fallbackTile == null)
                fallbackTile = CreateColoredTile(TileTheme.ColorFor(null));

            return fallbackTile;
        }

        private void OnDestroy()
        {
            // Destroy the ScriptableObject Tiles we generated (the inspector-assigned
            // defaultTile is an asset we don't own, so it's excluded below).
            foreach (var tile in tileCache.Values)
            {
                if (tile != null && tile != defaultTile)
                    Destroy(tile);
            }
            tileCache.Clear();

            if (fallbackTile != null && fallbackTile != defaultTile)
            {
                Destroy(fallbackTile);
                fallbackTile = null;
            }
        }

        private Tile CreateColoredTile(Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = GetSharedSprite();
            tile.color = color;
            // Drop the default LockColor so per-cell lighting (tilemap.SetColor) applies;
            // keep LockTransform so tiles are never accidentally offset or rotated.
            tile.flags = TileFlags.LockTransform;
            return tile;
        }

        private static Sprite GetSharedSprite()
        {
            if (sharedSprite == null)
            {
                sharedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                sharedTexture.SetPixel(0, 0, Color.white);
                sharedTexture.Apply();

                // pixelsPerUnit == 1 so the 1×1 sprite fills exactly one grid cell.
                sharedSprite = Sprite.Create(
                    sharedTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
            }

            return sharedSprite;
        }
    }
}
