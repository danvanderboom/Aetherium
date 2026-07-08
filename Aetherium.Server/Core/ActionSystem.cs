using System;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server
{
    /// <summary>
    /// Continuous, speed-based action scheduling (engine gap-analysis §4.1): each tick, refills
    /// every actor's <see cref="ActionSpeed"/> budget, then dispatches any <see cref="ActionQueue"/>'d
    /// action whose cost the (refilled) budget covers. An action that isn't yet affordable stays
    /// queued untouched for a later tick — there is no global turn order.
    /// <para/>
    /// Dispatch is delegated (not hard-wired to <see cref="World"/>/<see cref="CombatSystem"/>
    /// internals) so this class stays a pure, stateless-service like <see cref="CombatSystem"/>;
    /// callers (e.g. a future <c>GameMapGrain</c> wiring) supply how an Attack/Move actually
    /// resolves.
    /// </summary>
    public class ActionSystem
    {
        private readonly Func<World, Entity, string, CombatResult> _resolveAttack;
        private readonly Action<World, Entity, int, int> _resolveMove;

        public ActionSystem(Func<World, Entity, string, CombatResult> resolveAttack, Action<World, Entity, int, int> resolveMove)
        {
            _resolveAttack = resolveAttack ?? throw new ArgumentNullException(nameof(resolveAttack));
            _resolveMove = resolveMove ?? throw new ArgumentNullException(nameof(resolveMove));
        }

        /// <summary>Runs one tick of the action pipeline over every entity in <paramref name="world"/>.</summary>
        public void Tick(World world)
        {
            if (world == null) return;

            foreach (var entity in world.Entities.Values)
            {
                if (!entity.Has<ActionSpeed>())
                    continue;

                var speed = entity.Get<ActionSpeed>();
                speed.Refill();

                if (!entity.Has<ActionQueue>())
                    continue;

                var queue = entity.Get<ActionQueue>();
                if (!queue.TryPeek(out var action) || action is null)
                    continue;

                if (speed.Budget < action.ApCost)
                    continue; // Not affordable yet — stays queued for a later tick.

                queue.TryDequeue(out _);
                speed.Budget -= action.ApCost;

                Dispatch(world, entity, action);
            }
        }

        private void Dispatch(World world, Entity actor, QueuedAction action)
        {
            switch (action.Kind)
            {
                case ActionKind.Attack:
                    _resolveAttack(world, actor, action.TargetEntityId!);
                    break;
                case ActionKind.Move:
                    _resolveMove(world, actor, action.Dx, action.Dy);
                    break;
            }
        }
    }
}
