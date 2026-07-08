using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    public enum ActionKind
    {
        Move,
        Attack,
    }

    /// <summary>
    /// One intended action awaiting dispatch by the <c>ActionSystem</c>. Immutable — a queued
    /// action is either dispatched whole or left untouched for a later tick, never partially
    /// applied (engine gap-analysis §4.1).
    /// </summary>
    public class QueuedAction
    {
        public ActionKind Kind { get; }

        /// <summary>AP cost deducted from the actor's <see cref="ActionSpeed"/> budget on dispatch.</summary>
        public double ApCost { get; }

        /// <summary>Target entity id for <see cref="ActionKind.Attack"/>.</summary>
        public string? TargetEntityId { get; }

        /// <summary>Tile offset for <see cref="ActionKind.Move"/>.</summary>
        public int Dx { get; }
        public int Dy { get; }

        public QueuedAction(ActionKind kind, double apCost, string? targetEntityId = null, int dx = 0, int dy = 0)
        {
            Kind = kind;
            ApCost = apCost;
            TargetEntityId = targetEntityId;
            Dx = dx;
            Dy = dy;
        }

        public static QueuedAction Attack(string targetEntityId, double apCost = 1.0)
            => new(ActionKind.Attack, apCost, targetEntityId: targetEntityId);

        public static QueuedAction Move(int dx, int dy, double apCost = 1.0)
            => new(ActionKind.Move, apCost, dx: dx, dy: dy);
    }

    /// <summary>
    /// An actor's pending action(s), capped at <see cref="MaxDepth"/> (default 1 — no input
    /// buffering unless a game opts in; engine gap-analysis §4.1 multiplayer note).
    /// </summary>
    public class ActionQueue : Component
    {
        private readonly Queue<QueuedAction> _pending = new();

        public int MaxDepth { get; set; } = 1;

        public int Count => _pending.Count;

        public ActionQueue() { }

        public ActionQueue(int maxDepth)
        {
            MaxDepth = maxDepth;
        }

        /// <summary>Enqueues <paramref name="action"/>; fails if the queue is already at <see cref="MaxDepth"/>.</summary>
        public bool TryEnqueue(QueuedAction action)
        {
            if (_pending.Count >= MaxDepth)
                return false;

            _pending.Enqueue(action);
            return true;
        }

        public bool TryPeek(out QueuedAction? action)
            => _pending.TryPeek(out action);

        /// <summary>Removes and returns the head action. Callers dispatch it before calling this.</summary>
        public bool TryDequeue(out QueuedAction? action)
            => _pending.TryDequeue(out action);
    }
}
