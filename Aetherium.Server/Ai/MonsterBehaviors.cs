using System;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Ai
{
    /// <summary>
    /// A worked example of the behavior-tree engine (engine gap-analysis §4.5): attack an adjacent
    /// target if one exists, else wander. Built against the existing, live <see cref="CombatSystem"/>
    /// and <see cref="Monster.NextWanderDirection"/> so it reproduces exactly the decision
    /// <c>GameMapGrain.StepNpcsAsync</c> makes inline today — a candidate drop-in replacement for
    /// that inline logic, not yet wired in (see openspec/changes/add-npc-behavior-trees Phase 2).
    /// </summary>
    public static class MonsterBehaviors
    {
        public static BehaviorTree BuildWanderAndMeleeTree(CombatSystem combatSystem)
        {
            var attackIfAdjacent = new SequenceNode(
                new ConditionNode(ctx => FindAdjacentTarget(ctx) is not null),
                new ActionNode(ctx =>
                {
                    var target = FindAdjacentTarget(ctx);
                    var result = combatSystem.TryAttack(ctx.World, ctx.Self, target!.EntityId, removeOnDeath: false);
                    return result.Success ? BehaviorStatus.Success : BehaviorStatus.Failure;
                }));

            var wander = new ActionNode(ctx =>
            {
                if (ctx.Self is not Monster monster)
                    return BehaviorStatus.Failure;

                var direction = monster.NextWanderDirection();
                if (direction is null)
                    return BehaviorStatus.Failure;

                var outcome = ctx.World.TryMoveSteps(monster, direction.Value, 1);
                return outcome.Success ? BehaviorStatus.Success : BehaviorStatus.Failure;
            });

            return new BehaviorTree(new SelectorNode(attackIfAdjacent, wander));
        }

        /// <summary>Any other entity with <see cref="Health"/> within Manhattan distance 1 — mirrors
        /// <c>GameMapGrain.FindAdjacentPlayer</c>'s adjacency rule.</summary>
        private static Entity? FindAdjacentTarget(BehaviorContext ctx)
        {
            if (!ctx.Self.Has<WorldLocation>())
                return null;

            var selfLocation = ctx.Self.Get<WorldLocation>();

            return ctx.World.Entities.Values.FirstOrDefault(other =>
            {
                if (other.EntityId == ctx.Self.EntityId) return false;
                if (!other.Has<Health>() || !other.Has<WorldLocation>()) return false;

                var otherLocation = other.Get<WorldLocation>();
                int distance = Math.Abs(otherLocation.X - selfLocation.X)
                              + Math.Abs(otherLocation.Y - selfLocation.Y)
                              + Math.Abs(otherLocation.Z - selfLocation.Z);
                return distance <= 1;
            });
        }
    }
}
