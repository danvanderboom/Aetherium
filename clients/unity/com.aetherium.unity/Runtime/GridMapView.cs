using System;
using System.Collections.Generic;
using Aetherium.Client;
using Aetherium.Unity.Rounded;
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

        /// <summary>The content theme (terrain/region/creature bindings). Settable so mock/offline
        /// drivers and tests can configure the view without a scene-serialized reference.</summary>
        public ThemeAsset Theme { get => theme; set => theme = value; }

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

        [Header("Rounded region terrain")]
        [Tooltip("Chaikin smoothing passes for region coastlines (0 = blocky, 2-3 = organic).")]
        [SerializeField] private int regionSmoothIterations = 2;
        [Tooltip("Sub-grid tessellation density for the region SDF mesh (higher = smoother curve, more verts).")]
        [SerializeField] private int regionSubdivisions = 3;
        [Tooltip("Small vertical lift of region surfaces above the ground slabs so water never z-fights terrain.")]
        [SerializeField] private float regionYBias = 0.02f;
        [Tooltip("How far each region terrain feathers outward over its lower-priority neighbours " +
                 "(world units) — the soft blend at terrain boundaries. 0 = crisp edges, ~0.6 = fuzzy.")]
        [SerializeField] private float regionBlendWidth = 0.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP main color
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in main color

        private readonly Dictionary<GridPoint, CellView> _cells = new Dictionary<GridPoint, CellView>();
        private MaterialPropertyBlock _mpb;
        private AetheriumClientBehaviour _client;

        // Contiguous "region" terrains (water now; forest/mountain later) render as one smooth
        // curved mesh per terrain+level instead of a prefab per cell — see RoundedRegionRenderer.
        private readonly RoundedRegionRenderer _regionRenderer = new RoundedRegionRenderer();
        private readonly List<(string terrain, int x, int y, int z)> _regionCells =
            new List<(string, int, int, int)>();
        private Func<string, Material> _resolveRegionMaterial;
        private Func<string, int> _resolveRegionPriority;

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
            _resolveRegionMaterial = name => theme != null ? theme.ResolveRegionMaterial(name) : null;
            _resolveRegionPriority = name => theme != null ? theme.ResolveRegionPriority(name) : 0;
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

        /// <summary>A single cell to render, decoupled from the perception store so non-SignalR
        /// sources (offline/mock data, tests) can drive the view through the same path.</summary>
        public readonly struct CellInput
        {
            public readonly GridPoint Position;
            public readonly string TerrainName;
            public readonly bool InView;
            public readonly double LightLevel;

            public CellInput(GridPoint position, string terrainName, bool inView, double lightLevel)
            {
                Position = position;
                TerrainName = terrainName;
                InView = inView;
                LightLevel = lightLevel;
            }
        }

        private void SyncFromMemory()
        {
            var store = _client.Store;
            if (store == null)
                return;
            RenderCells(EnumerateMemory(store), store.Anchor, store.LatestFrame?.Topology);
        }

        private static IEnumerable<CellInput> EnumerateMemory(PerceptionStore store)
        {
            foreach (var remembered in store.Memory)
            {
                var name = remembered.Terrain != null ? remembered.Terrain.Name : null;
                if (name == null)
                    continue;
                yield return new CellInput(remembered.Position, name, remembered.InView, remembered.LastLightLevel);
            }
        }

        /// <summary>
        /// Renders an explicit cell set around <paramref name="anchor"/>. The live perception path
        /// funnels into this; it is public so offline/mock data and tests can drive the same
        /// rendering with no server. Region terrains (per the theme) draw as one smooth mesh on a
        /// square topology; everything else is a prefab per cell.
        /// </summary>
        public void RenderCells(IEnumerable<CellInput> cells, GridPoint anchor, string topology)
        {
            _mpb ??= new MaterialPropertyBlock();
            _resolveRegionMaterial ??= name => theme != null ? theme.ResolveRegionMaterial(name) : null;
            _resolveRegionPriority ??= name => theme != null ? theme.ResolveRegionPriority(name) : 0;

            long radiusSq = (long)renderRadius * renderRadius;

            // Region terrains (water/…) render as a smooth mesh, not prefabs — but the mesh math
            // assumes the square identity layout, so only route them there on a square topology;
            // any other topology keeps the classic prefab-per-cell path.
            bool regionsEnabled = theme != null && IsSquareTopology(topology);
            _regionCells.Clear();

            foreach (var cell in cells)
            {
                var terrainName = cell.TerrainName;
                if (terrainName == null)
                    continue;

                // Window cull: keep only the neighborhood live. Cells beyond the radius (or on
                // another level) are despawned; they re-materialize from memory if you walk back.
                long dx = cell.Position.X - anchor.X;
                long dy = cell.Position.Y - anchor.Y;
                bool near = cell.Position.Z == anchor.Z && (dx * dx + dy * dy) <= radiusSq;
                if (!near)
                {
                    if (_cells.TryGetValue(cell.Position, out var stale))
                    {
                        Destroy(stale.GameObject);
                        _cells.Remove(cell.Position);
                    }
                    continue;
                }

                if (regionsEnabled && theme.IsRegionTerrain(terrainName))
                {
                    // Drawn as part of the region mesh below; never spawn a prefab for it (and
                    // drop any prefab it had before it was classified as a region terrain).
                    if (_cells.TryGetValue(cell.Position, out var priorPrefab))
                    {
                        Destroy(priorPrefab.GameObject);
                        _cells.Remove(cell.Position);
                    }
                    _regionCells.Add((terrainName, cell.Position.X, cell.Position.Y, cell.Position.Z));
                    continue;
                }

                if (!_cells.TryGetValue(cell.Position, out var view))
                {
                    view = Materialize(cell.Position, terrainName);
                    _cells[cell.Position] = view;
                }
                else if (view.TerrainName != terrainName)
                {
                    // Terrain identity changed (door opened, wall breached): re-materialize.
                    Destroy(view.GameObject);
                    _cells[cell.Position] = view = Materialize(cell.Position, terrainName);
                }

                // In view: brightness follows the server's light level linearly — light
                // already attenuates linearly server-side, and a flatter curve here made
                // the gradient too subtle to read (the pool looked binary again, so
                // occlusion notches read as a broken disc instead of shadows). Out of
                // view: the flat memory dim.
                float target = cell.InView
                    ? Mathf.Lerp(inViewFloor, 1f, Mathf.Clamp01((float)cell.LightLevel))
                    : memoryDim;
                if (Mathf.Abs(view.Brightness - target) > 0.02f)
                {
                    view.Brightness = target;
                    ApplyDim(view, target);
                }
            }

            // One smooth mesh per (region terrain, level) covering the near cells collected above.
            if (regionsEnabled)
            {
                _regionRenderer.Sync(
                    transform, _regionCells, _resolveRegionMaterial,
                    cellSize, regionSmoothIterations, regionSubdivisions,
                    Color.white, regionYBias, _resolveRegionPriority, regionBlendWidth);
            }
            else
            {
                _regionRenderer.Clear();
            }
        }

        // The server defaults topology to "square"; treat null/empty as square too.
        private static bool IsSquareTopology(string topology) =>
            string.IsNullOrEmpty(topology) ||
            string.Equals(topology, "square", StringComparison.OrdinalIgnoreCase);

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
                var renderer = renderers[i];

                // Terrain cells never cast shadows. A ground map is thousands of near-coplanar
                // slabs; letting them cast into the directional shadow map makes them self-shadow
                // (shadow acne) and the cascade "swims" as the camera moves — the flickering
                // horizontal dark bands that sweep the field while walking. The world's actual
                // lighting is the server's per-cell brightness (applied below via the property
                // block), so the Unity shadow map is a redundant second lighting layer here; drop
                // the casters and the bands go with them. Entities (player/creatures) keep their
                // shadows — those come from EntityViewRegistry, not this view.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var shared = renderer.sharedMaterial;
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
            _regionRenderer.Clear();
        }
    }
}
