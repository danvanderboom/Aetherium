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
    /// dimming, while a translucent circle of the creature's color — starting at the
    /// creature's own footprint — expands smoothly outward on the floor (~1 cell of radius
    /// per second). The growing circle IS the positional uncertainty, "by now it could be
    /// anywhere in here" — so turning back to look shows you the memory rather than erasing
    /// it (an empty center cell disproves nothing once the circle outgrows it). Re-seeing
    /// the actual creature replaces its ghost instantly; otherwise both model and circle dim
    /// to nothing and disperse. A default a game can replace wholesale.</para>
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
        [Tooltip("How fast the uncertainty circle expands, in cells of radius per second — " +
                 "roughly how far the creature could have wandered by now.")]
        [SerializeField] private float ghostSpreadCellsPerSecond = 1f;
        [Tooltip("Starting diameter of the uncertainty circle, in cells — about the " +
                 "creature's own footprint.")]
        [SerializeField] private float ghostGlowStartCells = 0.8f;
        [Range(0f, 1f)]
        [Tooltip("Peak opacity of the uncertainty circle (it dims in lockstep with the " +
                 "fading model).")]
        [SerializeField] private float ghostGlowOpacity = 0.45f;

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
            public float SpawnTime;             // drives both the fade and the circle's growth
            public List<(Material Material, Color BaseColor)> BaseColors;
            public Color GlowColor;             // the creature's color, for the circle
            public Material GlowMaterial;       // the circle's tint (one color set per frame)
            public GameObject GlowDisc;         // the expanding uncertainty circle
        }

        private void Awake()
        {
            _client = GetComponent<AetheriumClientBehaviour>();
            _client.EntityAppeared += OnAppeared;
            _client.EntityMoved += OnMoved;
            _client.EntityVanished += OnVanished;
            _client.Reanchored += OnReanchored;
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
        /// dims while a translucent circle of the creature's color expands around it.</summary>
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
            var glowMaterial = CreateGlowMaterial(glowColor);
            _ghosts[entity.Id] = new GhostView
            {
                GameObject = instance,
                SpawnTime = Time.time,
                BaseColors = baseColors,
                GlowColor = glowColor,
                GlowMaterial = glowMaterial,
                GlowDisc = CreateGlowDisc(entity.Position, glowMaterial),
            };
        }

        /// <summary>The creature's own color for the uncertainty circle: the most saturated captured
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

        /// <summary>An unlit alpha-blended material for the uncertainty circle — unlit so it
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
            material.mainTexture = CircleTexture();
            material.color = color;
            return material;
        }

        /// <summary>A flat quad hovering just above the floor slab (whose top sits at the
        /// cell's y), textured with the crisp circle; Update scales it as uncertainty grows.</summary>
        private GameObject CreateGlowDisc(GridPoint cell, Material glowMaterial)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(disc.GetComponent<Collider>());
            disc.name = $"ghost-glow:{cell}";
            disc.transform.SetParent(transform, false);
            disc.transform.position = CellToWorld(cell) + new Vector3(0f, 0.02f, 0f);
            disc.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
            var renderer = disc.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return disc;
        }

        private static Texture2D _circleTexture;

        /// <summary>A shared runtime-generated circle: solid fill with a thin smoothstepped
        /// rim, so the disc reads as a crisp circle at any scale (the softness stays ~2% of
        /// the diameter). Generated once, never destroyed — every ghost's material shares it.</summary>
        private static Texture2D CircleTexture()
        {
            if (_circleTexture != null)
                return _circleTexture;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp, // no bleed past the quad's UV edge
                name = "AetheriumGhostCircle",
            };
            var pixels = new Color32[size * size];
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float alpha = 1f - Mathf.SmoothStep(0.96f, 1f, distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _circleTexture = texture;
            return texture;
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
            Destroy(ghost.GlowDisc);
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
            foreach (var (id, ghost) in _ghosts)
            {
                float elapsed = Time.time - ghost.SpawnTime;
                float fraction = ghostSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / ghostSeconds);
                if (fraction >= 1f)
                {
                    (expired ??= new List<string>()).Add(id);
                    continue;
                }

                // Dim model and circle in lockstep: brightest the instant it vanished,
                // gone entirely at the end of the window.
                float brightness = 1f - fraction;
                foreach (var (material, baseColor) in ghost.BaseColors)
                    material.color = new Color(
                        baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
                var glow = ghost.GlowColor;
                ghost.GlowMaterial.color = new Color(glow.r, glow.g, glow.b, brightness * ghostGlowOpacity);

                // Expand smoothly: the circle gains ~a cell of radius per second (tunable) —
                // the region the creature could plausibly have reached since you saw it.
                float diameter = (ghostGlowStartCells * 0.5f + elapsed * ghostSpreadCellsPerSecond)
                    * 2f * cellSize;
                ghost.GlowDisc.transform.localScale = new Vector3(diameter, diameter, 1f);
            }

            if (expired != null)
                foreach (var id in expired)
                    DestroyGhost(id);
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
