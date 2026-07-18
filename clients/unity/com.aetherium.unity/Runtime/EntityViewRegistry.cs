using System.Collections.Generic;
using Aetherium.Client;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// Materializes tracked entities as scene views: spawn on <c>EntityAppeared</c>, tween on
    /// <c>EntityMoved</c> (duration scaled so ≈1 Hz NPC steps read as deliberate motion, not
    /// teleports), despawn on <c>EntityVanished</c> — with a hook point for a death dissolve
    /// when the vanish was a confirmed kill.
    ///
    /// <para><b>Creature memory (ghosts):</b> a creature that leaves view (you turned away,
    /// it stepped past your lamp) does not blink out — the mind keeps a last-seen impression.
    /// Unlike terrain memory, a memory of a MOVING thing decays: the model lingers in place,
    /// dimming, while a circular pool of floor tiles glowing the creature's color spreads
    /// outward around it (~1 cell of radius per second) — the growing pool IS the positional
    /// uncertainty, "by now it could be anywhere in here". After a few seconds both dim to
    /// nothing and disperse. Looking back at the spot and seeing it empty collapses the ghost
    /// immediately: observation beats memory. A default a game can replace wholesale.</para>
    /// </summary>
    [RequireComponent(typeof(AetheriumClientBehaviour))]
    public sealed class EntityViewRegistry : MonoBehaviour
    {
        [SerializeField] private ThemeAsset theme;
        [Tooltip("World units per grid cell — keep equal to GridMapView's cellSize.")]
        [SerializeField] private float cellSize = 1f;
        [Tooltip("Seconds an entity slide takes; roughly the server NPC step interval.")]
        [SerializeField] private float tweenSeconds = 0.6f;
        [Tooltip("Render a player avatar pinned to the anchor. The local player is never a " +
                 "tracked entity (they are the perception origin), so without this you cannot " +
                 "see yourself.")]
        [SerializeField] private bool showPlayerAtAnchor = true;

        [Header("Creature memory (ghosts)")]
        [Tooltip("Seconds a last-seen creature impression lingers before dispersing entirely. " +
                 "Tune live in Play mode to taste — 4-6s are all reasonable reads.")]
        [SerializeField] private float ghostSeconds = 5f;
        [Tooltip("How fast the uncertainty glow pool spreads outward, in cells of radius per " +
                 "second — roughly how far the creature could have wandered by now.")]
        [SerializeField] private float ghostSpreadCellsPerSecond = 1f;
        [Range(0f, 1f)]
        [Tooltip("Peak opacity of the glowing uncertainty tiles (they dim in lockstep with " +
                 "the fading model).")]
        [SerializeField] private float ghostGlowOpacity = 0.45f;
        [Tooltip("Seconds for a DISPROVEN ghost to collapse (you looked at the spot and it's " +
                 "empty). Short: observation beats memory.")]
        [SerializeField] private float ghostCollapseSeconds = 0.3f;

        private readonly Dictionary<string, EntityView> _views = new Dictionary<string, EntityView>();
        private readonly Dictionary<string, GhostView> _ghosts = new Dictionary<string, GhostView>();
        private AetheriumClientBehaviour _client;
        private GameObject _playerAvatar;

        private sealed class EntityView
        {
            public GameObject GameObject;
            public Vector3 From;
            public Vector3 To;
            public float TweenStartTime;
        }

        private sealed class GhostView
        {
            public GameObject GameObject;       // the dimming last-seen model, in place
            public GridPoint Cell;              // last-seen client-space cell (for disproof)
            public float SpawnTime;             // fixed at creation; drives the glow spread
            public float StartTime;             // re-based on collapse; drives the fade
            public float Duration;              // remaining fade window (shrinks on collapse)
            public float StartFraction;         // fade progress when (re)based, for collapse
            public bool Collapsing;             // disproven: fade fast, stop spreading
            public List<(Material Material, Color BaseColor)> BaseColors;
            public Color GlowColor;             // the creature's color, for the tile pool
            public Material GlowMaterial;       // one shared material tints all this ghost's tiles
            public List<GameObject> GlowTiles = new List<GameObject>();
            public HashSet<GridPoint> GlowCells = new HashSet<GridPoint>();
            public int SpreadRadius;            // cells of glow currently materialized
        }

        private void Awake()
        {
            _client = GetComponent<AetheriumClientBehaviour>();
            _client.EntityAppeared += OnAppeared;
            _client.EntityMoved += OnMoved;
            _client.EntityVanished += OnVanished;
            _client.Reanchored += OnReanchored;
            _client.FrameReceived += _ => DisproveGhostsInView();
        }

        private Vector3 CellToWorld(GridPoint cell)
        {
            var topology = _client.Store?.LatestFrame?.Topology;
            var parity = _client.Store?.LatestFrame?.SelfCellParity;
            var anchor = _client.Store?.Anchor ?? GridPoint.Origin;
            // Client-space → layout position relative to the player, then into the scene.
            var (x, y) = Client.Contracts.GridCellLayout.CellLayoutPosition(
                topology, cell.X - anchor.X, cell.Y - anchor.Y, parity);
            var (ax, ay) = ((double)anchor.X, (double)anchor.Y);
            // Grid +Y is south; scene -Z is a natural "south" for a Y-up 3D scene.
            return new Vector3((float)((ax + x) * cellSize), cell.Z * cellSize, -(float)((ay + y) * cellSize));
        }

        private void OnAppeared(TrackedEntity entity)
        {
            // Seeing the real thing again supersedes any lingering memory of it.
            DestroyGhost(entity.Id);

            // A tracked character is always someone/something else — the local player is the
            // perception origin, rendered separately at the anchor. A character with no
            // Creature:<id> identity falls back to the creature default, not the player prefab.
            var prefab = entity.IsItem
                ? theme.ResolveItem(entity.Item != null ? entity.Item.Id : "")
                : theme.ResolveCreature(entity.CreatureTypeId ?? string.Empty);

            var instance = Instantiate(prefab, CellToWorld(entity.Position), Quaternion.identity, transform);
            instance.name = $"{(entity.IsItem ? "item" : "entity")}:{entity.Id}";
            instance.SetActive(true);
            _views[entity.Id] = new EntityView
            {
                GameObject = instance,
                From = instance.transform.position,
                To = instance.transform.position,
                TweenStartTime = Time.time,
            };
        }

        private void OnMoved(TrackedEntity entity, GridPoint from, GridPoint to)
        {
            if (!_views.TryGetValue(entity.Id, out var view))
                return;
            view.From = view.GameObject.transform.position;
            view.To = CellToWorld(to);
            view.TweenStartTime = Time.time;
        }

        private void OnVanished(TrackedEntity entity)
        {
            if (!_views.TryGetValue(entity.Id, out var view))
                return;
            _views.Remove(entity.Id);

            // A confirmed kill leaves no uncertainty (death dissolve VFX is the future hook);
            // items don't wander, so an item out of view carries no positional uncertainty
            // either. Only living creatures leave a decaying impression behind.
            if (entity.WasDefeated || entity.IsItem || ghostSeconds <= 0f)
            {
                Destroy(view.GameObject);
                return;
            }

            BecomeGhost(entity, view.GameObject);
        }

        /// <summary>Repurpose the live view as a last-seen impression: the model stays put and
        /// dims while a pool of tiles glowing the creature's color spreads around it.</summary>
        private void BecomeGhost(TrackedEntity entity, GameObject instance)
        {
            DestroyGhost(entity.Id); // paranoia: never two ghosts for one id
            instance.name = $"ghost:{entity.Id}";

            var baseColors = new List<(Material, Color)>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.materials) // per-instance; fine at M0 scale
                    if (material.HasProperty("_BaseColor") || material.HasProperty("_Color"))
                        baseColors.Add((material, material.color));

            var glowColor = PickGlowColor(baseColors);
            _ghosts[entity.Id] = new GhostView
            {
                GameObject = instance,
                Cell = entity.Position,
                SpawnTime = Time.time,
                StartTime = Time.time,
                Duration = ghostSeconds,
                StartFraction = 0f,
                BaseColors = baseColors,
                GlowColor = glowColor,
                GlowMaterial = CreateGlowMaterial(glowColor),
            };
        }

        /// <summary>The creature's own color for the glow pool: the most saturated captured
        /// base color (the stand-in capsules and tinted models carry identity here). Textured
        /// models often report plain white — no identity — so fall back to a hot ember.</summary>
        private static Color PickGlowColor(List<(Material Material, Color BaseColor)> baseColors)
        {
            var best = new Color(1f, 0.55f, 0.15f);
            float bestScore = 0.25f; // saturation×value floor below which a color reads as gray
            foreach (var (_, baseColor) in baseColors)
            {
                Color.RGBToHSV(baseColor, out _, out var s, out var v);
                if (s * v > bestScore)
                {
                    bestScore = s * v;
                    best = baseColor;
                }
            }
            return best;
        }

        /// <summary>An unlit alpha-blended material for the glow tiles — unlit so the pool
        /// reads as emission (memory-glow), not as a lit surface the lamp should affect.</summary>
        private static Material CreateGlowMaterial(Color color)
        {
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material material;
            if (urpUnlit != null)
            {
                material = new Material(urpUnlit);
                // The standard runtime recipe for a transparent URP surface.
                material.SetFloat("_Surface", 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material = new Material(Shader.Find("Sprites/Default")); // built-in fallback, alpha-blended
            }
            material.color = color;
            return material;
        }

        /// <summary>A flat quad hovering just above the floor slab (whose top sits at the
        /// cell's y), sharing the ghost's tint material so per-frame dimming is one color set.</summary>
        private GameObject CreateGlowTile(GridPoint cell, Material glowMaterial)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(tile.GetComponent<Collider>());
            tile.name = $"ghost-glow:{cell}";
            tile.transform.SetParent(transform, false);
            tile.transform.position = CellToWorld(cell) + new Vector3(0f, 0.02f, 0f);
            tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
            tile.transform.localScale = new Vector3(cellSize * 0.98f, cellSize * 0.98f, 1f);
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return tile;
        }

        /// <summary>Observation beats memory: a ghost whose cell is currently IN VIEW — and
        /// whose creature demonstrably isn't there — is disproven; collapse it fast.</summary>
        private void DisproveGhostsInView()
        {
            if (_ghosts.Count == 0)
                return;
            var store = _client.Store;
            if (store == null)
                return;

            List<string> disproven = null;
            foreach (var (id, ghost) in _ghosts)
            {
                if (ghost.Duration <= ghostCollapseSeconds)
                    continue; // already collapsing
                foreach (var cell in store.Memory)
                {
                    if (cell.Position == ghost.Cell && cell.InView)
                    {
                        (disproven ??= new List<string>()).Add(id);
                        break;
                    }
                }
            }
            if (disproven == null)
                return;

            foreach (var id in disproven)
            {
                // Re-base the fade so it finishes within the collapse window from HERE,
                // preserving the current visual state (no pop back to full brightness).
                var ghost = _ghosts[id];
                ghost.StartFraction = GhostFraction(ghost);
                ghost.StartTime = Time.time;
                ghost.Duration = ghostCollapseSeconds;
                ghost.Collapsing = true; // a disproven memory stops spreading too
            }
        }

        private static float GhostFraction(GhostView ghost)
        {
            float t = ghost.Duration <= 0f ? 1f : (Time.time - ghost.StartTime) / ghost.Duration;
            return Mathf.Clamp01(ghost.StartFraction + (1f - ghost.StartFraction) * Mathf.Clamp01(t));
        }

        private void DestroyGhost(string entityId)
        {
            if (_ghosts.TryGetValue(entityId, out var ghost))
            {
                _ghosts.Remove(entityId);
                DestroyGhostObjects(ghost);
            }
        }

        private static void DestroyGhostObjects(GhostView ghost)
        {
            Destroy(ghost.GameObject);
            foreach (var tile in ghost.GlowTiles)
                Destroy(tile);
            Destroy(ghost.GlowMaterial); // runtime-created; Unity never collects these itself
        }

        private void OnReanchored(ReanchorReason reason)
        {
            // A discontinuity means every view's position is stale — hard cut, don't tween.
            foreach (var view in _views.Values)
                Destroy(view.GameObject);
            _views.Clear();
            foreach (var ghost in _ghosts.Values)
                DestroyGhostObjects(ghost);
            _ghosts.Clear();
        }

        private void Update()
        {
            foreach (var view in _views.Values)
            {
                float t = tweenSeconds <= 0f ? 1f : Mathf.Clamp01((Time.time - view.TweenStartTime) / tweenSeconds);
                view.GameObject.transform.position = Vector3.Lerp(view.From, view.To, t);
            }

            UpdateGhosts();
            UpdatePlayerAvatar();
        }

        private void UpdateGhosts()
        {
            if (_ghosts.Count == 0)
                return;

            List<string> expired = null;
            HashSet<GridPoint> wallCells = null; // built lazily, shared by every ghost this frame
            foreach (var (id, ghost) in _ghosts)
            {
                float fraction = GhostFraction(ghost);
                if (fraction >= 1f)
                {
                    (expired ??= new List<string>()).Add(id);
                    continue;
                }

                // Dim model and glow pool in lockstep: brightest the instant it vanished,
                // gone entirely at the end of the window.
                float brightness = 1f - fraction;
                foreach (var (material, baseColor) in ghost.BaseColors)
                    material.color = new Color(
                        baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
                var glow = ghost.GlowColor;
                ghost.GlowMaterial.color = new Color(glow.r, glow.g, glow.b, brightness * ghostGlowOpacity);

                // Spread: the pool gains ~a cell of radius per second (tunable) — the region
                // the creature could plausibly have reached since you last saw it.
                int radius = Mathf.FloorToInt((Time.time - ghost.SpawnTime) * ghostSpreadCellsPerSecond);
                if (!ghost.Collapsing && radius > ghost.SpreadRadius)
                {
                    wallCells ??= CollectRememberedWallCells();
                    GrowGlowPool(ghost, radius, wallCells);
                }
            }

            if (expired != null)
                foreach (var id in expired)
                    DestroyGhost(id);
        }

        /// <summary>Materialize the glow disc out to <paramref name="radius"/>: every cell
        /// within a circular (Euclidean) radius, minus the center (the model stands there)
        /// and minus known walls — the creature can't be inside a wall, and glow on top of
        /// wall blocks would read as bleed-through.</summary>
        private void GrowGlowPool(GhostView ghost, int radius, HashSet<GridPoint> wallCells)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    // +0.45 rounds the disc: radius 1 covers all 8 neighbors, radius 2
                    // takes knight-distance cells but not the far corners — reads circular.
                    if (Mathf.Sqrt(dx * dx + dy * dy) > radius + 0.45f)
                        continue;
                    var cell = new GridPoint(ghost.Cell.X + dx, ghost.Cell.Y + dy, ghost.Cell.Z);
                    if (!ghost.GlowCells.Add(cell) || wallCells.Contains(cell))
                        continue;
                    ghost.GlowTiles.Add(CreateGlowTile(cell, ghost.GlowMaterial));
                }
            ghost.SpreadRadius = radius;
        }

        private HashSet<GridPoint> CollectRememberedWallCells()
        {
            var walls = new HashSet<GridPoint>();
            var store = _client.Store;
            if (store == null)
                return walls;
            foreach (var remembered in store.Memory)
            {
                var name = remembered.Terrain != null ? remembered.Terrain.Name : null;
                if (name != null && name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    walls.Add(remembered.Position);
            }
            return walls;
        }

        /// <summary>
        /// Keep a player avatar glued to the anchor and facing the current heading. The local
        /// player is the perception origin — never in the frame's entity list — so this is the
        /// only thing that shows you where you are.
        /// </summary>
        private void UpdatePlayerAvatar()
        {
            var store = _client.Store;
            if (!showPlayerAtAnchor || theme == null || store == null || store.LatestFrame == null)
                return;

            if (_playerAvatar == null)
            {
                _playerAvatar = Instantiate(theme.ResolvePlayer(), transform);
                _playerAvatar.name = "player:self";
                _playerAvatar.SetActive(true);
            }

            _playerAvatar.transform.position = CellToWorld(store.Anchor);
            // HeadingDegrees is compass-clockwise; scene north=+Z east=+X, so Unity yaw = heading.
            _playerAvatar.transform.rotation = Quaternion.Euler(0f, store.LatestFrame.HeadingDegrees, 0f);
        }
    }
}
