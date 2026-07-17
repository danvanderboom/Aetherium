using System.Collections.Generic;
using Aetherium.Client;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// Materializes tracked entities as scene views: spawn on <c>EntityAppeared</c>, tween on
    /// <c>EntityMoved</c> (duration scaled so ≈1 Hz NPC steps read as deliberate motion, not
    /// teleports), despawn on <c>EntityVanished</c> — with a hook point for a death dissolve
    /// when the vanish was a confirmed kill. A default a game can replace wholesale.
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

        private readonly Dictionary<string, EntityView> _views = new Dictionary<string, EntityView>();
        private AetheriumClientBehaviour _client;
        private GameObject _playerAvatar;

        private sealed class EntityView
        {
            public GameObject GameObject;
            public Vector3 From;
            public Vector3 To;
            public float TweenStartTime;
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
            // Hook point: entity.WasDefeated distinguishes a kill (death dissolve VFX)
            // from simply leaving perception (fade). M0 default: destroy either way.
            Destroy(view.GameObject);
        }

        private void OnReanchored(ReanchorReason reason)
        {
            // A discontinuity means every view's position is stale — hard cut, don't tween.
            foreach (var view in _views.Values)
                Destroy(view.GameObject);
            _views.Clear();
        }

        private void Update()
        {
            foreach (var view in _views.Values)
            {
                float t = tweenSeconds <= 0f ? 1f : Mathf.Clamp01((Time.time - view.TweenStartTime) / tweenSeconds);
                view.GameObject.transform.position = Vector3.Lerp(view.From, view.To, t);
            }

            UpdatePlayerAvatar();
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
