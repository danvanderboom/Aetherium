using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Client.Contracts;

namespace Aetherium.Client
{
    /// <summary>Why the store re-anchored (views should hard-cut, not tween across it).</summary>
    public enum ReanchorReason
    {
        Joined,
        Respawned,
        Portal,
        LevelChanged,
    }

    /// <summary>A visible entity (character or item) tracked across frames in client-space.</summary>
    public sealed class TrackedEntity
    {
        public string Id { get; internal set; } = string.Empty;
        public bool IsItem { get; internal set; }
        /// <summary>Creature content id parsed from "Creature:&lt;id&gt;" names; null for players/items.</summary>
        public string? CreatureTypeId { get; internal set; }
        public CharacterDto? Character { get; internal set; }
        public ItemDto? Item { get; internal set; }
        public GridPoint Position { get; internal set; }
        public DateTime FirstSeenUtc { get; internal set; }
        // Presentation state perception doesn't carry (folded from attack results):
        public int? LastKnownHealth { get; internal set; }
        public bool WasDefeated { get; internal set; }
    }

    /// <summary>Last-seen terrain for a client-space cell — the memory layer that lets a game
    /// render explored-but-dark areas distinctly.</summary>
    public sealed class RememberedCell
    {
        public GridPoint Position { get; internal set; }
        public TileTypeDto? Terrain { get; internal set; }
        public double LastLightLevel { get; internal set; }
        public DateTime LastSeenUtc { get; internal set; }
        public bool InView { get; internal set; }
    }

    /// <summary>
    /// The heart of the client core (docs/design/unity-sample/unity-client-library.md): consumes
    /// the server's full player-relative perception frames and gives games three things the wire
    /// deliberately doesn't carry:
    /// <list type="number">
    /// <item><b>Anchoring</b> — a stable client-space coordinate frame. The anchor starts at the
    /// join position and advances by the player's own successful movement; each frame's relative
    /// offsets are added to it. Relative offsets are world-axis-aligned (north-up), never
    /// heading-rotated. Discontinuities (portal, respawn) re-anchor and raise <see cref="Reanchored"/>.</item>
    /// <item><b>Frame diffing</b> — entity lifecycle events (<see cref="EntityAppeared"/> /
    /// <see cref="EntityMoved"/> / <see cref="EntityVanished"/>) derived by diffing consecutive
    /// frames by entity id. Vanished means <i>left perception</i> — died OR walked into darkness;
    /// the store doesn't pretend to know, but attack results fold in so your own kills are marked.</item>
    /// <item><b>Memory</b> — last-seen terrain per client-space cell with timestamps.</item>
    /// </list>
    /// Thread-agnostic and lock-protected; events may fire on SignalR worker threads — marshalling
    /// to a main thread is the adapter's job (the Unity layer), never the core's.
    /// </summary>
    public sealed class PerceptionStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, TrackedEntity> _entities = new Dictionary<string, TrackedEntity>();
        private readonly Dictionary<GridPoint, RememberedCell> _memory = new Dictionary<GridPoint, RememberedCell>();
        private GridPoint _anchor = GridPoint.Origin;
        private PerceptionDto? _latest;

        /// <summary>The most recent raw frame (player-relative, exactly as received).</summary>
        public PerceptionDto? LatestFrame { get { lock (_gate) return _latest; } }

        /// <summary>The player's current client-space cell.</summary>
        public GridPoint Anchor { get { lock (_gate) return _anchor; } }

        public event Action<PerceptionDto>? FrameReceived;
        public event Action<TrackedEntity>? EntityAppeared;
        public event Action<TrackedEntity, GridPoint, GridPoint>? EntityMoved;
        public event Action<TrackedEntity>? EntityVanished;
        public event Action<RememberedCell>? CellRevealed;
        public event Action<ReanchorReason>? Reanchored;

        /// <summary>Snapshot of currently-visible tracked entities.</summary>
        public IReadOnlyList<TrackedEntity> Entities
        {
            get { lock (_gate) return _entities.Values.ToList(); }
        }

        /// <summary>Snapshot of remembered terrain (both in-view and explored-but-dark cells).</summary>
        public IReadOnlyList<RememberedCell> Memory
        {
            get { lock (_gate) return _memory.Values.ToList(); }
        }

        /// <summary>
        /// Advances the anchor by the player's own successful movement — called by the
        /// ToolClient after a successful move/changelevel with the world-space delta it
        /// computed from the heading and steps taken.
        /// </summary>
        public void AdvanceAnchor(int dx, int dy, int dz)
        {
            lock (_gate)
                _anchor = _anchor.Offset(dx, dy, dz);
        }

        /// <summary>
        /// Declares a positional discontinuity (portal, respawn, join): re-bases the anchor to
        /// origin, wipes entity tracking and remembered geometry the client can no longer
        /// trust, and raises <see cref="Reanchored"/> so views hard-cut instead of tweening.
        /// </summary>
        public void NoteDiscontinuity(ReanchorReason reason)
        {
            lock (_gate)
            {
                _anchor = GridPoint.Origin;
                _entities.Clear();
                _memory.Clear();
                _latest = null;
            }
            Reanchored?.Invoke(reason);
        }

        /// <summary>
        /// Folds an attack result into presentation state: last-known health for the target,
        /// and — when <paramref name="defeated"/> — marks the entity so its next vanish reads
        /// as a kill (death VFX) rather than an unexplained disappearance.
        /// </summary>
        public void NoteAttackResult(string targetEntityId, int? remainingHealth, bool defeated)
        {
            lock (_gate)
            {
                if (!_entities.TryGetValue(targetEntityId, out var entity))
                    return;
                if (remainingHealth.HasValue)
                    entity.LastKnownHealth = remainingHealth.Value;
                if (defeated)
                    entity.WasDefeated = true;
            }
        }

        /// <summary>
        /// Applies an incoming frame: updates memory, diffs entities against the previous
        /// frame, and raises lifecycle events (outside the lock, in a deterministic order:
        /// cells, then appears, moves, vanishes, then <see cref="FrameReceived"/>).
        /// </summary>
        public void ApplyFrame(PerceptionDto frame)
        {
            if (frame is null) throw new ArgumentNullException(nameof(frame));

            var revealed = new List<RememberedCell>();
            var appeared = new List<TrackedEntity>();
            var moved = new List<(TrackedEntity Entity, GridPoint From, GridPoint To)>();
            var vanished = new List<TrackedEntity>();

            lock (_gate)
            {
                _latest = frame;
                var now = DateTime.UtcNow;
                var anchor = _anchor;

                // --- memory layer: fold in every visible cell ---
                var inViewNow = new HashSet<GridPoint>();
                foreach (var pair in frame.Visuals)
                {
                    if (!TryParseRelativeKey(pair.Key, out var rel))
                        continue;
                    var cell = anchor.Offset(rel.X, rel.Y, rel.Z);
                    inViewNow.Add(cell);

                    if (!_memory.TryGetValue(cell, out var remembered))
                    {
                        remembered = new RememberedCell { Position = cell };
                        _memory[cell] = remembered;
                        revealed.Add(remembered);
                    }
                    remembered.Terrain = pair.Value.Terrain ?? remembered.Terrain;
                    remembered.LastLightLevel = pair.Value.LightLevel;
                    remembered.LastSeenUtc = now;
                    remembered.InView = true;
                }
                foreach (var remembered in _memory.Values)
                    if (!inViewNow.Contains(remembered.Position))
                        remembered.InView = false;

                // --- entity diff by id across characters + items ---
                var seenIds = new HashSet<string>();

                foreach (var character in frame.VisibleCharacters)
                {
                    if (string.IsNullOrEmpty(character.Id) || character.Location is null)
                        continue;
                    seenIds.Add(character.Id);
                    var position = anchor.Offset(character.Location.X, character.Location.Y, character.Location.Z);

                    if (_entities.TryGetValue(character.Id, out var tracked))
                    {
                        var from = tracked.Position;
                        tracked.Character = character;
                        tracked.Position = position;
                        if (from != position)
                            moved.Add((tracked, from, position));
                    }
                    else
                    {
                        tracked = new TrackedEntity
                        {
                            Id = character.Id,
                            IsItem = false,
                            CreatureTypeId = ParseCreatureTypeId(character),
                            Character = character,
                            Position = position,
                            FirstSeenUtc = now,
                        };
                        _entities[character.Id] = tracked;
                        appeared.Add(tracked);
                    }
                }

                foreach (var item in frame.VisibleItems)
                {
                    if (string.IsNullOrEmpty(item.Id) || item.Location is null)
                        continue;
                    seenIds.Add(item.Id);
                    var position = anchor.Offset(item.Location.X, item.Location.Y, item.Location.Z);

                    if (_entities.TryGetValue(item.Id, out var tracked))
                    {
                        var from = tracked.Position;
                        tracked.Item = item;
                        tracked.Position = position;
                        if (from != position)
                            moved.Add((tracked, from, position));
                    }
                    else
                    {
                        tracked = new TrackedEntity
                        {
                            Id = item.Id,
                            IsItem = true,
                            Item = item,
                            Position = position,
                            FirstSeenUtc = now,
                        };
                        _entities[item.Id] = tracked;
                        appeared.Add(tracked);
                    }
                }

                foreach (var id in _entities.Keys.Where(id => !seenIds.Contains(id)).ToList())
                {
                    vanished.Add(_entities[id]);
                    _entities.Remove(id);
                }
            }

            foreach (var cell in revealed) CellRevealed?.Invoke(cell);
            foreach (var entity in appeared) EntityAppeared?.Invoke(entity);
            foreach (var (entity, from, to) in moved) EntityMoved?.Invoke(entity, from, to);
            foreach (var entity in vanished) EntityVanished?.Invoke(entity);
            FrameReceived?.Invoke(frame);
        }

        /// <summary>Creature identity contract: "Creature:&lt;contentId&gt;" on Name/Tile.Name.</summary>
        internal static string? ParseCreatureTypeId(CharacterDto character)
        {
            const string prefix = "Creature:";
            var name = character.Name;
            if (name != null && name.StartsWith(prefix, StringComparison.Ordinal))
                return name.Substring(prefix.Length);
            var tileName = character.Tile?.Name;
            if (tileName != null && tileName.StartsWith(prefix, StringComparison.Ordinal))
                return tileName.Substring(prefix.Length);
            return null;
        }

        /// <summary>Parses a "relX,relY,relZ" perception key.</summary>
        internal static bool TryParseRelativeKey(string key, out (int X, int Y, int Z) rel)
        {
            rel = default;
            if (string.IsNullOrEmpty(key))
                return false;
            var parts = key.Split(',');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var x)
                || !int.TryParse(parts[1], out var y)
                || !int.TryParse(parts[2], out var z))
                return false;
            rel = (x, y, z);
            return true;
        }
    }
}
