using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server;
using Aetherium.WorldBuilders;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Validates that <see cref="InteractionSystem.TryLockpick"/> routes its
    /// probabilistic decision through the injected <see cref="IRandomSource"/>
    /// so tests get deterministic outcomes (no <c>new Random()</c> per call).
    /// </summary>
    public class LockpickDeterminismTests
    {
        private static (GameSession session, LockpickItem pick, Door door) NewLockpickScenario(IRandomSource random)
        {
            var session = new GameSession("lockpick-test", new FovDiagnosticWorldBuilder("open_space"));
            // Place a locked door adjacent to the player so context evaluation lets us target it.
            var door = new Door();
            var pl = session.Player!.Get<WorldLocation>()!;
            door.Set(new WorldLocation(pl.X + 1, pl.Y, pl.Z));
            door.Get<OpensAndCloses>()!.IsLocked = true;
            session.World.AddEntity(door);

            var pick = new LockpickItem(skillLevel: 1);
            session.Player.Get<Inventory>()!.TryAdd(pick.EntityId, pick);
            return (session, pick, door);
        }

        [Fact]
        public void Roll_Zero_Forces_Success_And_Decrements_Durability()
        {
            // Skill 1 → success chance 0.65; roll 0.0 < 0.65 ⇒ success.
            var rng = new FixedRandomSource(0.0);
            var system = new InteractionSystem(rng);
            var (session, pick, door) = NewLockpickScenario(rng);
            var initialDurability = pick.Get<Lockpick>()!.Durability;

            var result = system.TryLockpick(session, pick.EntityId, door.EntityId);

            Assert.True(result.Success);
            Assert.False(door.Get<OpensAndCloses>()!.IsLocked);
            Assert.Equal(initialDurability - 1, pick.Get<Lockpick>()!.Durability);
        }

        [Fact]
        public void Roll_Above_Threshold_Forces_Failure_And_Door_Stays_Locked()
        {
            // Skill 1 → success chance 0.65; roll 0.99 ≥ 0.65 ⇒ failure.
            var rng = new FixedRandomSource(0.99);
            var system = new InteractionSystem(rng);
            var (session, pick, door) = NewLockpickScenario(rng);
            var initialDurability = pick.Get<Lockpick>()!.Durability;

            var result = system.TryLockpick(session, pick.EntityId, door.EntityId);

            Assert.False(result.Success);
            Assert.True(door.Get<OpensAndCloses>()!.IsLocked);
            // Failure path also decrements durability.
            Assert.Equal(initialDurability - 1, pick.Get<Lockpick>()!.Durability);
        }

        [Fact]
        public void Repeated_Failures_Break_Pick_When_Durability_Hits_Zero()
        {
            // All rolls fail. Lockpick starts at durability 15; after 15 failures
            // the lockpick is destroyed and removed from inventory.
            var rng = new FixedRandomSource(0.99);
            var system = new InteractionSystem(rng);
            var (session, pick, door) = NewLockpickScenario(rng);
            var inv = session.Player!.Get<Inventory>()!;

            InteractionResult? last = null;
            for (int i = 0; i < 15; i++)
                last = system.TryLockpick(session, pick.EntityId, door.EntityId);

            Assert.NotNull(last);
            Assert.False(last!.Success);
            Assert.Equal("Lockpick broke", last.Reason);
            Assert.False(inv.Items.ContainsKey(pick.EntityId));
        }

        [Fact]
        public void Default_RandomSource_Is_Used_When_Constructor_Argument_Is_Omitted()
        {
            // Smoke test: parameterless ctor must not throw and TryLockpick should
            // not blow up. We don't assert outcome (it's truly random) — only that
            // the call path works without an explicit IRandomSource.
            var system = new InteractionSystem();
            var (session, pick, door) = NewLockpickScenario(new DefaultRandomSource());

            var result = system.TryLockpick(session, pick.EntityId, door.EntityId);

            // Either success or one of the documented failure reasons.
            Assert.True(result.Success || result.Reason == "Lockpicking failed" || result.Reason == "Lockpick broke");
        }
    }
}
