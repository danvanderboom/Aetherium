using System.Collections.Generic;
using Aetherium.Unity.Model;
using Aetherium.Unity.Networking;
using Aetherium.Unity.Spatial;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Aetherium.Unity.Rendering
{
    /// <summary>
    /// Renders PerceptionLite data to a Unity 2D Tilemap with Z-level support.
    /// </summary>
    public class TilemapRenderer2D : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private TileBase defaultTile;
        private Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();
        private TileBase fallbackTile;

        private int currentZLevel = 0;
        private PerceptionLite? currentPerception;

        // Cells written in the previous render pass. RenderPerception only clears
        // cells in (previous \ current) and only writes the (current) batch, so we
        // avoid ClearAllTiles + per-tile SetTile every frame.
        private readonly HashSet<Vector3Int> previousCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> currentCells = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> positionsScratch = new List<Vector3Int>();
        private readonly List<TileBase> tilesScratch = new List<TileBase>();

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

            foreach (var visual in perception.Visuals.Values)
            {
                if (visual.Location.Z != zLevel)
                    continue;

                var worldPos = GridHelpers.GridToWorld(visual.Location);
                var cellPos = tilemap.WorldToCell(worldPos);
                if (!currentCells.Add(cellPos))
                    continue; // duplicate visual at the same cell, ignore

                positionsScratch.Add(cellPos);
                tilesScratch.Add(GetOrCreateTile(visual.TileTypeId, perception.TileTypes));
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

            // Try to get tile type info
            if (tileTypes.TryGetValue(tileTypeId, out var tileType))
            {
                var tile = CreateTileFromType(tileType);
                tileCache[tileTypeId] = tile;
                return tile;
            }

            // Fallback to default
            var fallback = GetFallbackTile();
            tileCache[tileTypeId] = fallback;
            return fallback;
        }

        private TileBase GetFallbackTile()
        {
            if (defaultTile != null)
                return defaultTile;

            // Lazily create a single fallback Tile so we don't allocate per frame
            // when no inspector-assigned defaultTile is provided.
            if (fallbackTile == null)
                fallbackTile = CreateDefaultTile();

            return fallbackTile;
        }

        private void OnDestroy()
        {
            if (fallbackTile != null)
            {
                Destroy(fallbackTile);
                fallbackTile = null;
            }
        }

        private TileBase CreateTileFromType(TileTypeLite tileType)
        {
            // For now, create a simple colored tile
            // In a full implementation, this could load sprites from Resources or addressables
            return CreateDefaultTile();
        }

        private TileBase CreateDefaultTile()
        {
            // Create a simple white tile
            var tile = ScriptableObject.CreateInstance<Tile>();
            // Note: Tile sprite would need to be set from a Sprite asset
            // For now, this is a placeholder that Unity will handle
            return tile;
        }
    }
}
