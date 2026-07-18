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
    /// Unlike terrain memory, a memory of a MOVING thing decays: the view lingers as a
    /// darkening, slowly expanding ghost (the expansion reads as positional uncertainty — it
    /// could have moved anywhere within the fade window), then disperses. Looking back at the
    /// spot and seeing it empty collapses the ghost immediately: observation beats memory.
    /// A default a game can replace wholesale.</para>
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
                 "Tune live in Play mode to taste — 4-10s are all reasonable reads.")]
        [SerializeField] private float ghostSeconds = 6f;
        [Tooltip("How many cells of positional uncertainty the ghost expands by over its full " +
                 "fade — a fast creature could be roughly this far away by the time it's gone.")]
        [SerializeField] private float ghostExpansionCells = 1.5f;
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
            public GameObject GameObject;
            public GridPoint Cell;              // last-seen client-space cell (for disproof)
            public float StartTime;
            public float Duration;              // remaining fade window (shrinks on collapse)
            public float StartFraction;         // fade progress when (re)based, for collapse
            public Vector3 BaseScale;
            public List<(Material Material, Color BaseColor)> BaseColors;
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

        /// <summary>Repurpose the live view as a last-seen impression: captured base colors
        /// darken to nothing while the model slowly expands (positional uncertainty).</summary>
        private void BecomeGhost(TrackedEntity entity, GameObject instance)
        {
            DestroyGhost(entity.Id); // paranoia: never two ghosts for one id
            instance.name = $"ghost:{entity.Id}";

            var baseColors = new List<(Material, Color)>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.materials) // per-instance; fine at M0 scale
                    if (material.HasProperty("_BaseColor") || material.HasProperty("_Color"))
                        baseColors.Add((material, material.color));

            _ghosts[entity.Id] = new GhostView
            {
                GameObject = instance,
                Cell = entity.Position,
                StartTime = Time.time,
                Duration = ghostSeconds,
                StartFraction = 0f,
                BaseScale = instance.transform.localScale,
                BaseColors = baseColors,
            };
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
                Destroy(ghost.GameObject);
            }
        }

        private void OnReanchored(ReanchorReason reason)
        {
            // A discontinuity means every view's position is stale — hard cut, don't tween.
            foreach (var view in _views.Values)
                Destroy(view.GameObject);
            _views.Clear();
            foreach (var ghost in _ghosts.Values)
                Destroy(ghost.GameObject);
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
                float fraction = GhostFraction(ghost);
                if (fraction >= 1f)
                {
                    (expired ??= new List<string>()).Add(id);
                    continue;
                }

                // Disperse: brightness falls to zero while the silhouette swells by the
                // configured positional uncertainty — "it was here, it could be anywhere
                // within this radius by now".
                float brightness = 1f - fraction;
                foreach (var (material, baseColor) in ghost.BaseColors)
                    material.color = new Color(
                        baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);

                float swell = 1f + fraction * ghostExpansionCells;
                ghost.GameObject.transform.localScale = ghost.BaseScale * swell;
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
