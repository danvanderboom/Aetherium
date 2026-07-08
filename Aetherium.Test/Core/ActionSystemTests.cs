using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;
using Aetherium.Server;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Core
{
    /// <summary>
    /// Unit coverage of the continuous action pipeline's ECS primitives (engine gap-analysis
    /// §4.1, Phase 1 — see openspec/changes/add-continuous-action-pipeline). Not yet wired into
    /// any live grain/command path; these tests exercise <see cref="ActionSystem"/> in isolation.
    /// Verifies "Action Budget", "Action Queue", and "Action Tick Scheduling" in
    /// specs/engine-core/spec.md.
    /// </summary>
    [TestFixture]
    public class ActionSystemTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character NewActor(World world, double budget, double speed, double maxBudget)
        {
            var actor = new Character();
            actor.Set(new WorldLocation(0, 0, 0));
            actor.Set(new ActionSpeed(speed, maxBudget, budget));
            actor.Set(new ActionQueue());
            world.AddEntity(actor);
            return actor;
        }

        [Test]
        public void ActionSpeed_Refill_AddsSpeed_CappedAtMax()
        {
            var speed = new ActionSpeed(speed: 0.5, maxBudget: 1.0, budget: 0.3);
            speed.Refill();
            Assert.That(speed.Budget, Is.EqualTo(0.8).Within(1e-9));

            speed.Refill();
            Assert.That(speed.Budget, Is.EqualTo(1.0).Within(1e-9), "Refill must not exceed MaxBudget.");
        }

        // Verifies "Action Budget" (TrySpend) in specs/engine-core/spec.md
        // (openspec/changes/wire-npc-action-budget-live).
        [Test]
        public void TrySpend_RefillsThenSpends_WhenAffordable()
        {
            var speed = new ActionSpeed(speed: 1.0, maxBudget: 1.0, budget: 0.0);

            // Refill brings 0.0 → 1.0, which covers a cost of 1.0: spend succeeds, leaving 0.0.
            Assert.That(speed.TrySpend(1.0), Is.True);
            Assert.That(speed.Budget, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void TrySpend_Defers_WhenUnaffordable_ButRetainsRefilledAp()
        {
            var speed = new ActionSpeed(speed: 0.5, maxBudget: 1.0, budget: 0.0);

            // Refill brings 0.0 → 0.5, which does NOT cover a cost of 1.0: spend fails, but the
            // refilled 0.5 is retained (accrues toward a later tick), not discarded.
            Assert.That(speed.TrySpend(1.0), Is.False);
            Assert.That(speed.Budget, Is.EqualTo(0.5).Within(1e-9), "A failed TrySpend must keep the refilled AP.");
        }

        [Test]
        public void TrySpend_HalfSpeedActor_AffordsEveryOtherTick()
        {
            // A Speed-0.5 actor with cost 1.0 and Budget starting full (1.0): affords tick 1
            // (refill caps at 1.0, spend → 0.0), defers tick 2 (0.5 < 1.0), affords tick 3
            // (0.5 + 0.5 = 1.0). This is the differential cadence the pipeline exists to give.
            var speed = new ActionSpeed(speed: 0.5, maxBudget: 1.0);

            Assert.That(speed.TrySpend(1.0), Is.True,  "Tick 1: full budget affords.");
            Assert.That(speed.TrySpend(1.0), Is.False, "Tick 2: only 0.5 accrued, cannot afford.");
            Assert.That(speed.TrySpend(1.0), Is.True,  "Tick 3: accrued back to 1.0, affords again.");
            Assert.That(speed.TrySpend(1.0), Is.False, "Tick 4: defers again — a 2-tick cadence.");
        }

        [Test]
        public void ActionQueue_Enqueue_RejectsBeyondMaxDepth()
        {
            var queue = new ActionQueue(maxDepth: 1);
            Assert.That(queue.TryEnqueue(QueuedAction.Move(1, 0)), Is.True);
            Assert.That(queue.TryEnqueue(QueuedAction.Move(0, 1)), Is.False, "Depth cap must reject a second enqueue.");

            queue.TryPeek(out var head);
            Assert.That(head!.Dx, Is.EqualTo(1), "Original queued action must be unchanged after a rejected enqueue.");
        }

        [Test]
        public void Tick_DispatchesAffordableAction_AndRemovesFromQueue()
        {
            var world = NewWorld();
            var actor = NewActor(world, budget: 1.0, speed: 0.0, maxBudget: 1.0);
            actor.Get<ActionQueue>().TryEnqueue(QueuedAction.Move(1, 0, apCost: 0.5));

            int moveCalls = 0;
            var system = new ActionSystem(
                resolveAttack: (w, a, t) => CombatResult.Fail("unused"),
                resolveMove: (w, a, dx, dy) => moveCalls++);

            system.Tick(world);

            Assert.That(moveCalls, Is.EqualTo(1));
            Assert.That(actor.Get<ActionQueue>().Count, Is.EqualTo(0), "Dispatched action must leave the queue.");
            Assert.That(actor.Get<ActionSpeed>().Budget, Is.EqualTo(0.5).Within(1e-9), "Cost must be deducted after refill.");
        }

        [Test]
        public void Tick_DefersUnaffordableAction_QueueAndBudgetUnchanged()
        {
            var world = NewWorld();
            var actor = NewActor(world, budget: 0.2, speed: 0.0, maxBudget: 1.0);
            actor.Get<ActionQueue>().TryEnqueue(QueuedAction.Move(1, 0, apCost: 1.0));

            int moveCalls = 0;
            var system = new ActionSystem(
                resolveAttack: (w, a, t) => CombatResult.Fail("unused"),
                resolveMove: (w, a, dx, dy) => moveCalls++);

            system.Tick(world);

            Assert.That(moveCalls, Is.EqualTo(0), "Unaffordable action must not dispatch.");
            Assert.That(actor.Get<ActionQueue>().Count, Is.EqualTo(1), "Action must remain queued.");
            Assert.That(actor.Get<ActionSpeed>().Budget, Is.EqualTo(0.2).Within(1e-9), "Budget must be unchanged by a deferred dispatch.");
        }

        [Test]
        public void Tick_RefillsBudget_BeforeCheckingAffordability()
        {
            var world = NewWorld();
            // Budget starts below cost, but Speed refills it to exactly enough this tick.
            var actor = NewActor(world, budget: 0.5, speed: 0.5, maxBudget: 1.0);
            actor.Get<ActionQueue>().TryEnqueue(QueuedAction.Move(1, 0, apCost: 1.0));

            int moveCalls = 0;
            var system = new ActionSystem(
                resolveAttack: (w, a, t) => CombatResult.Fail("unused"),
                resolveMove: (w, a, dx, dy) => moveCalls++);

            system.Tick(world);

            Assert.That(moveCalls, Is.EqualTo(1), "Refill must happen before the affordability check in the same tick.");
            Assert.That(actor.Get<ActionSpeed>().Budget, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void Tick_AttackAction_DispatchesThroughCombatDelegate_WithCorrectTarget()
        {
            var world = NewWorld();
            var actor = NewActor(world, budget: 1.0, speed: 0.0, maxBudget: 1.0);
            var target = new Character();
            target.Set(new WorldLocation(1, 0, 0));
            world.AddEntity(target);

            actor.Get<ActionQueue>().TryEnqueue(QueuedAction.Attack(target.EntityId, apCost: 1.0));

            Entity? capturedActor = null;
            string? capturedTarget = null;
            var system = new ActionSystem(
                resolveAttack: (w, a, t) => { capturedActor = a; capturedTarget = t; return CombatResult.Hit(5, 10, false, "Character", t); },
                resolveMove: (w, a, dx, dy) => { });

            system.Tick(world);

            Assert.That(capturedActor, Is.SameAs(actor));
            Assert.That(capturedTarget, Is.EqualTo(target.EntityId));
        }

        [Test]
        public void Tick_EntityWithoutActionSpeed_IsIgnored()
        {
            var world = NewWorld();
            var bystander = new Character();
            bystander.Set(new WorldLocation(2, 2, 0));
            world.AddEntity(bystander);

            var system = new ActionSystem(
                resolveAttack: (w, a, t) => CombatResult.Fail("unused"),
                resolveMove: (w, a, dx, dy) => Assert.Fail("Move should not be invoked for an entity with no ActionSpeed."));

            Assert.DoesNotThrow(() => system.Tick(world));
        }
    }
}
