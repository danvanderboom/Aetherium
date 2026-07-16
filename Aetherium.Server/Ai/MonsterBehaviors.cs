using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Ai
{
    /// <summary>Reports what a tick of <see cref="MonsterBehaviors.BuildWanderAndMeleeTree"/> did, so a
    /// caller that needs to emit deltas (e.g. <c>GameMapGrain.StepNpcsAsync</c>) doesn't have to
    /// re-derive it — the tree itself only returns a <see cref="BehaviorStatus"/>.</summary>
    public readonly record struct AttackOutcome(string TargetEntityId, int RemainingHealth);
    public readonly record struct WanderOutcome(WorldLocation From, WorldLocation To);

    /// <summary>
    /// A worked example of the behavior-tree engine (engine gap-analysis §4.5): attack an adjacent
    /// target if one exists, else wander. Built against the existing, live <see cref="CombatSystem"/>
    /// and <see cref="Monster.NextWanderDirection"/> so it reproduces exactly the decision
    /// <c>GameMapGrain.StepNpcsAsync</c> makes inline today. Wired into that method's live NPC tick
    /// (see openspec/changes/add-npc-behavior-trees Phase 2) — one tree instance is owned per monster
    /// so blackboard/composite state persists across ticks per the "Per-NPC Behavior Tree Instance"
    /// requirement.
    /// </summary>
    public static class MonsterBehaviors
    {
        /// <summary>Blackboard key for the caller-supplied list of valid attack targets (e.g. joined
        /// players). When absent, <see cref="FindAdjacentTarget"/> falls back to scanning every entity
        /// with <see cref="Health"/> in the world — the original Phase 1 worked-example behavior, kept
        /// so existing unit tests that don't populate the blackboard are unaffected.</summary>
        public const string TargetsKey = "Targets";

        /// <summary>Blackboard key an attack action writes to on success, so the caller can build the
        /// health-changed delta without re-resolving the hit.</summary>
        public const string AttackOutcomeKey = "AttackOutcome";

        /// <summary>Blackboard key a wander action writes to on success, so the caller can build the
        /// entity-moved delta without re-resolving the step.</summary>
        public const string MoveOutcomeKey = "MoveOutcome";

        public static BehaviorTree BuildWanderAndMeleeTree(CombatSystem combatSystem)
        {
            var attackIfAdjacent = new SequenceNode(
                new ConditionNode(ctx => FindAdjacentTarget(ctx) is not null),
                new ActionNode(ctx =>
                {
                    var target = FindAdjacentTarget(ctx);
                    var result = combatSystem.TryAttack(ctx.World, ctx.Self, target!.EntityId, removeOnDeath: false);
                    if (result.Success)
                        ctx.Blackboard.Set(AttackOutcomeKey, new AttackOutcome(target!.EntityId, result.RemainingHealth));
                    return result.Success ? BehaviorStatus.Success : BehaviorStatus.Failure;
                }));

            var wander = new ActionNode(ctx =>
            {
                if (ctx.Self is not Monster monster)
                    return BehaviorStatus.Failure;
                if (!monster.Has<WorldLocation>())
                    return BehaviorStatus.Failure;

                var direction = monster.NextWanderDirection();
                if (direction is null)
                    return BehaviorStatus.Failure;

                var before = monster.Get<WorldLocation>();
                var outcome = ctx.World.TryMoveSteps(monster, direction.Value, 1);
                if (!outcome.Success || outcome.FinalLocation is null)
                    return BehaviorStatus.Failure;

                ctx.Blackboard.Set(MoveOutcomeKey, new WanderOutcome(before, outcome.FinalLocation));
                return BehaviorStatus.Success;
            });

            return new BehaviorTree(new SelectorNode(attackIfAdjacent, wander));
        }

        /// <summary>The nearest valid target within Manhattan distance 1 — mirrors
        /// <c>GameMapGrain</c>'s former <c>FindAdjacentPlayer</c> adjacency rule. Searches the
        /// blackboard's <see cref="TargetsKey"/> list when the caller supplied one (live wiring always
        /// does, scoping monsters to attacking joined players only — never each other); otherwise
        /// falls back to any entity with <see cref="Health"/> in the world.</summary>
        private static Entity? FindAdjacentTarget(BehaviorContext ctx)
        {
            if (!ctx.Self.Has<WorldLocation>())
                return null;

            var selfLocation = ctx.Self.Get<WorldLocation>();

            IEnumerable<Entity> candidates = ctx.Blackboard.TryGet<IReadOnlyList<Entity>>(TargetsKey, out var targets)
                ? targets
                : ctx.World.Entities.Values;

            return candidates.FirstOrDefault(other =>
            {
                if (other.EntityId == ctx.Self.EntityId) return false;
                if (!other.Has<Health>() || !other.Has<WorldLocation>()) return false;

                var otherLocation = other.Get<WorldLocation>();
                // Topology metric on the plane + vertical steps (the Z axis is
                // engine-level, not topology's) — on square this is the Manhattan
                // distance this rule has always used.
                int distance = ctx.World.Topology.Distance(
                                   Aetherium.Topology.GridCoord.From(selfLocation),
                                   Aetherium.Topology.GridCoord.From(otherLocation))
                             + Math.Abs(otherLocation.Z - selfLocation.Z);
                return distance <= 1;
            });
        }
    }
}
