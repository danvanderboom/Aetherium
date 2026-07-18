using System.Collections.Generic;
using Aetherium.Client;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// Reveals terrain as placed prefabs (docs/design/unity-sample/unity-client-library.md):
    /// every remembered cell near the player gets a view at its client-space position, converted
    /// to local space through the shared, drift-tested layout math (square identity, hex
    /// pointy-top, triangle parity) — so client visuals and server geometry always agree about
    /// where a cell is. Cells that slip out of view dim (explored-but-dark memory rendering); a
    /// re-anchor clears everything for a hard cut. A default a game can replace wholesale.
    ///
    /// <para><b>Bounded to the neighborhood.</b> The client only materializes cells within
    /// <see cref="renderRadius"/> of the player and pools everything beyond it, so the live
    /// GameObject count is a function of the view window — <em>not</em> of how much of the world
    /// you have explored. This is what keeps movement responsive on very large worlds: perception
    /// is already O(view) server-side, and rendering must be O(view) too, never O(map) or
    /// O(explored-area). Per-cell brightness is pushed through a <see cref="MaterialPropertyBlock"/>
    /// on a shared, instanced material rather than by cloning a material per cell — so thousands of
    /// tiles collapse into a handful of GPU-instanced draw calls instead of thousands of unbatchable
    /// ones (the original per-cell <c>renderer.materials</c> clone was the main frame-rate sink).</para>
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
        [Range(0f, 1f)]
        [Tooltip("Brightness of the dimmest in-view cell. In-view brightness scales with the " +
                 "server's light level between this and 1, so the lamp pool renders as a " +
                 "gradient instead of a hard-edged disc (shadow clipping then reads as " +
                 "lighting, not a malformed circle).")]
        [SerializeField] private float inViewFloor = 0.3f;
        [Tooltip("Only cells within this many grid cells of the player are kept as live " +
                 "GameObjects; anything farther is despawned and re-materialized if you return. " +
                 "Bounds render cost to the neighborhood so world size doesn't affect frame rate. " +
                 "Keep this >= the server vision range (game.yaml player.vision.range) plus a small " +
                 "margin, or explored-but-dark cells will pop at the edge of view.")]
        [SerializeField] private int renderRadius = 26;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP main color
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in main color

        private readonly Dictionary<GridPoint, CellView> _cells = new Dictionary<GridPoint, CellView>();
        private MaterialPropertyBlock _mpb;
        private AetheriumClientBehaviour _client;

        private sealed class CellView
        {
            public GameObject GameObject;
            public string TerrainName;
            public float Brightness = -1f; // unset; first sync always applies
            public Renderer[] Renderers;
            /// <summary>The prefab's shared base color per renderer, captured at creation so
            /// brightness is an absolute assignment (multiplying in place could never brighten
            /// back). Read from the SHARED material — the view never clones a material.</summary>
            public Color[] BaseColors;
        }

        private void Awake()
        {
            _client = GetComponent<AetheriumClientBehaviour>();
            _mpb = new MaterialPropertyBlock();
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

            var anchor = store.Anchor;
            long radiusSq = (long)renderRadius * renderRadius;

            foreach (var remembered in store.Memory)
            {
                var terrainName = remembered.Terrain != null ? remembered.Terrain.Name : null;
                if (terrainName == null)
                    continue;

                // Window cull: keep only the neighborhood live. Cells beyond the radius (or on
                // another level) are despawned; they re-materialize from memory if you walk back.
                long dx = remembered.Position.X - anchor.X;
                long dy = remembered.Position.Y - anchor.Y;
                bool near = remembered.Position.Z == anchor.Z && (dx * dx + dy * dy) <= radiusSq;
                if (!near)
                {
                    if (_cells.TryGetValue(remembered.Position, out var stale))
                    {
                        Destroy(stale.GameObject);
                        _cells.Remove(remembered.Position);
                    }
                    continue;
                }

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

                // In view: brightness follows the server's light level linearly — light
                // already attenuates linearly server-side, and a flatter curve here made
                // the gradient too subtle to read (the pool looked binary again, so
                // occlusion notches read as a broken disc instead of shadows). Out of
                // view: the flat memory dim.
                float target = remembered.InView
                    ? Mathf.Lerp(inViewFloor, 1f, Mathf.Clamp01((float)remembered.LastLightLevel))
                    : memoryDim;
                if (Mathf.Abs(cell.Brightness - target) > 0.02f)
                {
                    cell.Brightness = target;
                    ApplyDim(cell, target);
                }
            }
        }

        private CellView Materialize(GridPoint position, string terrainName)
        {
            var prefab = theme.ResolveTerrain(terrainName);
            var instance = Instantiate(prefab, CellToWorld(position), Quaternion.identity, transform);
            instance.name = $"cell:{position}";
            instance.SetActive(true);

            // Capture base colors from the SHARED material (never .materials/.material, whose
            // getters clone a unique material per cell and make every tile its own unbatchable
            // draw call). Per-cell brightness rides a MaterialPropertyBlock instead; enabling
            // instancing on the shared material lets those per-cell colors GPU-instance into one
            // draw call per terrain type.
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterial;
                if (shared != null)
                {
                    shared.enableInstancing = true;
                    baseColors[i] = shared.HasProperty(BaseColorId) ? shared.GetColor(BaseColorId)
                        : shared.HasProperty(ColorId) ? shared.GetColor(ColorId)
                        : Color.white;
                }
                else
                {
                    baseColors[i] = Color.white;
                }
            }

            return new CellView
            {
                GameObject = instance,
                TerrainName = terrainName,
                Renderers = renderers,
                BaseColors = baseColors,
            };
        }

        private void ApplyDim(CellView cell, float brightness)
        {
            for (int i = 0; i < cell.Renderers.Length; i++)
            {
                var renderer = cell.Renderers[i];
                if (renderer == null)
                    continue;
                var baseColor = cell.BaseColors[i];
                var color = new Color(
                    baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);

                renderer.GetPropertyBlock(_mpb);
                var shared = renderer.sharedMaterial;
                if (shared == null || shared.HasProperty(BaseColorId)) _mpb.SetColor(BaseColorId, color);
                if (shared != null && shared.HasProperty(ColorId)) _mpb.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_mpb);
            }
        }

        private void Clear()
        {
            foreach (var cell in _cells.Values)
                Destroy(cell.GameObject);
            _cells.Clear();
        }
    }
}
