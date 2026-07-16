using System.Collections.Generic;
using Aetherium.Client;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// Reveals terrain as placed prefabs (docs/design/unity-sample/unity-client-library.md):
    /// every remembered cell gets a view at its client-space position, converted to local
    /// space through the shared, drift-tested layout math (square identity, hex pointy-top,
    /// triangle parity) — so client visuals and server geometry always agree about where a
    /// cell is. Cells that slip out of view dim (explored-but-dark memory rendering); a
    /// re-anchor clears everything for a hard cut. A default a game can replace wholesale.
    /// </summary>
    [RequireComponent(typeof(AetheriumClientBehaviour))]
    public sealed class GridMapView : MonoBehaviour
    {
        [SerializeField] private ThemeAsset theme;
        [Tooltip("World units per grid cell — keep equal to EntityViewRegistry's cellSize.")]
        [SerializeField] private float cellSize = 1f;
        [Range(0f, 1f)]
        [Tooltip("Brightness multiplier for remembered (out-of-view) cells.")]
        [SerializeField] private float memoryDim = 0.35f;

        private readonly Dictionary<GridPoint, CellView> _cells = new Dictionary<GridPoint, CellView>();
        private AetheriumClientBehaviour _client;

        private sealed class CellView
        {
            public GameObject GameObject;
            public string TerrainName;
            public bool Dimmed;
            /// <summary>Original material colors, captured at creation so dim/undim is
            /// an absolute assignment (multiplying in place could never brighten back).</summary>
            public List<(Material Material, Color BaseColor)> BaseColors;
        }

        private void Awake()
        {
            _client = GetComponent<AetheriumClientBehaviour>();
            _client.FrameReceived += _ => SyncFromMemory();
            _client.Reanchored += _ => Clear();
        }

        private Vector3 CellToWorld(GridPoint cell)
        {
            var frame = _client.Store?.LatestFrame;
            var anchor = _client.Store?.Anchor ?? GridPoint.Origin;
            var (x, y) = Client.Contracts.GridCellLayout.CellLayoutPosition(
                frame?.Topology, cell.X - anchor.X, cell.Y - anchor.Y, frame?.SelfCellParity);
            return new Vector3(
                (float)((anchor.X + x) * cellSize),
                cell.Z * cellSize,
                -(float)((anchor.Y + y) * cellSize));
        }

        private void SyncFromMemory()
        {
            var store = _client.Store;
            if (store == null)
                return;

            foreach (var remembered in store.Memory)
            {
                var terrainName = remembered.Terrain != null ? remembered.Terrain.Name : null;
                if (terrainName == null)
                    continue;

                if (!_cells.TryGetValue(remembered.Position, out var cell))
                {
                    cell = Materialize(remembered.Position, terrainName);
                    _cells[remembered.Position] = cell;
                }
                else if (cell.TerrainName != terrainName)
                {
                    // Terrain identity changed (door opened, wall breached): re-materialize.
                    Destroy(cell.GameObject);
                    _cells[remembered.Position] = cell = Materialize(remembered.Position, terrainName);
                }

                bool shouldDim = !remembered.InView;
                if (cell.Dimmed != shouldDim)
                {
                    cell.Dimmed = shouldDim;
                    ApplyDim(cell, shouldDim ? memoryDim : 1f);
                }
            }
        }

        private CellView Materialize(GridPoint position, string terrainName)
        {
            var prefab = theme.ResolveTerrain(terrainName);
            var instance = Instantiate(prefab, CellToWorld(position), Quaternion.identity, transform);
            instance.name = $"cell:{position}";
            instance.SetActive(true);

            // Capture base colors once so dim/undim assigns absolute values.
            var baseColors = new List<(Material, Color)>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.materials) // per-instance; fine at M0 scale
                    if (material.HasProperty("_Color"))
                        baseColors.Add((material, material.color));

            return new CellView { GameObject = instance, TerrainName = terrainName, BaseColors = baseColors };
        }

        private static void ApplyDim(CellView cell, float brightness)
        {
            foreach (var (material, baseColor) in cell.BaseColors)
                material.color = new Color(
                    baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
        }

        private void Clear()
        {
            foreach (var cell in _cells.Values)
                Destroy(cell.GameObject);
            _cells.Clear();
        }
    }
}
