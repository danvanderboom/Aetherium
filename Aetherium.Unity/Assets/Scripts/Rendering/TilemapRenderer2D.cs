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
        [SerializeField] private Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();

        private int currentZLevel = 0;
        private PerceptionLite? currentPerception;

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

            tilemap.ClearAllTiles();

            // Render only tiles on the current Z-level
            foreach (var visual in perception.Visuals.Values)
            {
                if (visual.Location.Z == zLevel)
                {
                    var tile = GetOrCreateTile(visual.TileTypeId, perception.TileTypes);
                    var worldPos = GridHelpers.GridToWorld(visual.Location);
                    var cellPos = tilemap.WorldToCell(worldPos);
                    tilemap.SetTile(cellPos, tile);
                }
            }
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
        /// Gets the number of tiles rendered at the current Z-level.
        /// </summary>
        public int GetRenderedTileCount()
        {
            if (tilemap == null || currentPerception == null)
                return 0;

            int count = 0;
            foreach (var visual in currentPerception.Visuals.Values)
            {
                if (visual.Location.Z == currentZLevel)
                {
                    count++;
                }
            }
            return count;
        }

        private TileBase GetOrCreateTile(string tileTypeId, Dictionary<string, TileTypeLite> tileTypes)
        {
            if (string.IsNullOrEmpty(tileTypeId))
            {
                return defaultTile ?? CreateDefaultTile();
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
            var defaultTileBase = defaultTile ?? CreateDefaultTile();
            tileCache[tileTypeId] = defaultTileBase;
            return defaultTileBase;
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

