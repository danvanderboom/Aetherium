using System;
using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Ai;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Ai
{
    /// <summary>
    /// Unit coverage of the generic behavior-tree engine (engine gap-analysis §4.5, Phase 1 — see
    /// openspec/changes/add-npc-behavior-trees). Game-agnostic: no world/entity dependency needed
    /// beyond the minimal <see cref="BehaviorContext"/>.
    /// Verifies "Behavior Tree Node Vocabulary" and "Per-NPC Behavior Tree Instance" in
    /// specs/npc-behavior-trees/spec.md.
    /// </summary>
    [TestFixture]
    public class BehaviorNodeTests
    {
        private static BehaviorContext NewContext()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            var self = new Character();
            self.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(self);
            return new BehaviorContext(world, self, new Blackboard());
        }

        [Test]
        public void ConditionNode_MapsPredicateToSuccessOrFailure()
        {
            Assert.That(new ConditionNode(_ => true).Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(new ConditionNode(_ => false).Tick(NewContext()), Is.EqualTo(BehaviorStatus.Failure));
        }

        [Test]
        public void ActionNode_ReturnsWhateverTheDelegateReturns()
        {
            var node = new ActionNode(_ => BehaviorStatus.Running);
            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Running));
        }

        [Test]
        public void WaitNode_RunsForNTicks_ThenSucceeds()
        {
            var node = new WaitNode(3);
            var ctx = NewContext();

            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Running));
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Running));
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void WaitNode_ZeroTicks_SucceedsImmediately()
        {
            var node = new WaitNode(0);
            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void WaitNode_CanRunAgainAfterCompleting()
        {
            var node = new WaitNode(1);
            var ctx = NewContext();
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Success), "Wait must restart cleanly after completing.");
        }

        [Test]
        public void SequenceNode_AllSucceed_ReturnsSuccess()
        {
            var node = new SequenceNode(
                new ActionNode(_ => BehaviorStatus.Success),
                new ActionNode(_ => BehaviorStatus.Success));

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void SequenceNode_OneFails_ReturnsFailure_AndResets()
        {
            int secondChildCalls = 0;
            var node = new SequenceNode(
                new ActionNode(_ => BehaviorStatus.Failure),
                new ActionNode(_ => { secondChildCalls++; return BehaviorStatus.Success; }));

            var ctx = NewContext();
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Failure));
            Assert.That(secondChildCalls, Is.EqualTo(0), "A failed first child must short-circuit the sequence.");
        }

        [Test]
        public void SequenceNode_RunningChild_ResumesAtSameIndexNextTick()
        {
            int firstCalls = 0;
            var node = new SequenceNode(
                new ActionNode(_ => { firstCalls++; return firstCalls < 2 ? BehaviorStatus.Running : BehaviorStatus.Success; }),
                new ActionNode(_ => BehaviorStatus.Success));

            var ctx = NewContext();
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Running));
            Assert.That(node.Tick(ctx), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(firstCalls, Is.EqualTo(2), "Running must resume the same child, not restart from index 0.");
        }

        [Test]
        public void SelectorNode_FirstSuccessWins_RemainingSkipped()
        {
            int secondChildCalls = 0;
            var node = new SelectorNode(
                new ActionNode(_ => BehaviorStatus.Success),
                new ActionNode(_ => { secondChildCalls++; return BehaviorStatus.Success; }));

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(secondChildCalls, Is.EqualTo(0));
        }

        [Test]
        public void SelectorNode_AllFail_ReturnsFailure()
        {
            var node = new SelectorNode(
                new ActionNode(_ => BehaviorStatus.Failure),
                new ActionNode(_ => BehaviorStatus.Failure));

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Failure));
        }

        [Test]
        public void ParallelNode_DefaultRequiresAllChildren_Success()
        {
            var node = new ParallelNode(new BehaviorNode[]
            {
                new ActionNode(_ => BehaviorStatus.Success),
                new ActionNode(_ => BehaviorStatus.Success),
            });

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void ParallelNode_OneFailure_MakesRequireAllImpossible_Fails()
        {
            var node = new ParallelNode(new BehaviorNode[]
            {
                new ActionNode(_ => BehaviorStatus.Success),
                new ActionNode(_ => BehaviorStatus.Failure),
            });

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Failure));
        }

        [Test]
        public void ParallelNode_PartialRequiredSuccesses_SucceedsEvenWithOneFailure()
        {
            var node = new ParallelNode(new BehaviorNode[]
            {
                new ActionNode(_ => BehaviorStatus.Success),
                new ActionNode(_ => BehaviorStatus.Failure),
            }, requiredSuccesses: 1);

            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Success));
        }

        [Test]
        public void RandomSelectorNode_PicksOneChild_AndSticksWithItWhileRunning()
        {
            int aCalls = 0, bCalls = 0;
            var node = new RandomSelectorNode(new Random(42),
                new ActionNode(_ => { aCalls++; return BehaviorStatus.Running; }),
                new ActionNode(_ => { bCalls++; return BehaviorStatus.Running; }));

            var ctx = NewContext();
            node.Tick(ctx);
            node.Tick(ctx);
            node.Tick(ctx);

            // Exactly one of the two children should have been called every time (3 total calls
            // to whichever was picked), never a mix.
            Assert.That(aCalls + bCalls, Is.EqualTo(3));
            Assert.That(aCalls == 3 || bCalls == 3, Is.True, "RandomSelectorNode must stick with its picked child while Running.");
        }

        [Test]
        public void UtilitySelectorNode_PicksHighestScoringChild()
        {
            var node = new UtilitySelectorNode(
                (new ActionNode(_ => BehaviorStatus.Success), _ => 1.0),
                (new ActionNode(_ => BehaviorStatus.Failure), _ => 5.0));

            // The second child (score 5.0) must be the one ticked, so the result is Failure.
            Assert.That(node.Tick(NewContext()), Is.EqualTo(BehaviorStatus.Failure));
        }

        // Verifies "Per-NPC Behavior Tree Instance" (openspec/changes/add-npc-behavior-trees/specs/npc-behavior-trees/spec.md).
        [Test]
        public void TwoTreeInstances_FromTheSameStructure_DoNotShareRunningState()
        {
            static BehaviorTree BuildTree(Func<BehaviorContext, BehaviorStatus> firstChildAction)
                => new(new SequenceNode(
                    new ActionNode(firstChildAction),
                    new ActionNode(_ => BehaviorStatus.Success)));

            int aCalls = 0, bCalls = 0;
            var treeA = BuildTree(_ => { aCalls++; return aCalls < 2 ? BehaviorStatus.Running : BehaviorStatus.Success; });
            var treeB = BuildTree(_ => { bCalls++; return BehaviorStatus.Success; });

            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            var self = new Character();
            self.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(self);

            // Tree A's Sequence is left mid-way (Running) on its first child.
            Assert.That(treeA.Tick(world, self), Is.EqualTo(BehaviorStatus.Running));

            // Tree B, built from an identical node structure, must start from its own first child
            // rather than inheriting Tree A's in-progress index.
            Assert.That(treeB.Tick(world, self), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(bCalls, Is.EqualTo(1), "Tree B's first child must have been ticked exactly once, independent of Tree A's progress.");

            // Tree A resumes at its own first child (not restarted) and now completes.
            Assert.That(treeA.Tick(world, self), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(aCalls, Is.EqualTo(2));
        }
    }
}
