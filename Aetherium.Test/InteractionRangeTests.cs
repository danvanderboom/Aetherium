using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;
using Aetherium.Server;
using Xunit;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for the interaction reach rule (P0-3 in docs/audits/RECOMMENDATIONS.md):
    /// direct physical interactions — open/close, unlock, lockpick, force, activate —
    /// must fail when the target is not at or cardinally adjacent to the actor on the
    /// same Z level. Previously any entity could be acted on by ID map-wide.
    /// </summary>
    public class InteractionRangeTests
    {
        private static GameSession CreateSession() =>
            new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));

        private static Door PlaceDoor(GameSession session, WorldLocation at, bool locked = false, string? keyShape = null)
        {
            var door = new Door();
            door.Set(at);
            var oc = door.Get<OpensAndCloses>();
            oc.IsLocked = locked;
            if (keyShape != null)
                oc.KeyShape = keyShape;
            session.World.AddEntity(door);
            return door;
        }

        [Fact]
        public void TryOpen_Fails_When_Door_Not_Adjacent()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var door = PlaceDoor(session, new WorldLocation(22, 15, 0)); // 7 cells away

            var result = new InteractionSystem().TryOpen(session, door.EntityId);

            Assert.False(result.Success);
            Assert.Contains("far", result.Reason.ToLowerInvariant());
            Assert.False(door.Get<OpensAndCloses>().IsOpen);
        }

        [Fact]
        public void TryOpen_Succeeds_When_Adjacent()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var door = PlaceDoor(session, new WorldLocation(16, 15, 0)); // adjacent

            var result = new InteractionSystem().TryOpen(session, door.EntityId);

            Assert.True(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsOpen);
        }

        [Fact]
        public void TryClose_Fails_When_Door_Not_Adjacent()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var door = PlaceDoor(session, new WorldLocation(15, 25, 0));
            door.Get<OpensAndCloses>().IsOpen = true;

            var result = new InteractionSystem().TryClose(session, door.EntityId);

            Assert.False(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsOpen); // unchanged
        }

        [Fact]
        public void TryOpen_Fails_Across_Z_Levels()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var door = PlaceDoor(session, new WorldLocation(15, 15, 1)); // same X/Y, other floor

            var result = new InteractionSystem().TryOpen(session, door.EntityId);

            Assert.False(result.Success);
        }

        [Fact]
        public void UnlockDoor_ViaUsageId_Fails_At_Range()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var key = new KeyItem("red");
            session.Player!.Get<Inventory>()!.TryAdd(key.EntityId, key);
            var door = PlaceDoor(session, new WorldLocation(25, 15, 0), locked: true, keyShape: "red");

            // Direct TryUseWithMode bypasses GetUseOptions' context filtering —
            // this is exactly the map-wide-unlock hole the reach rule closes.
            var result = new InteractionSystem().TryUseWithMode(session, key.EntityId, door.EntityId, "unlock-door");

            Assert.False(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsLocked); // still locked
        }

        [Fact]
        public void UnlockDoor_ViaUsageId_Succeeds_When_Adjacent()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var key = new KeyItem("red");
            session.Player!.Get<Inventory>()!.TryAdd(key.EntityId, key);
            var door = PlaceDoor(session, new WorldLocation(16, 15, 0), locked: true, keyShape: "red");

            var result = new InteractionSystem().TryUseWithMode(session, key.EntityId, door.EntityId, "unlock-door");

            Assert.True(result.Success);
            Assert.False(door.Get<OpensAndCloses>().IsLocked);
        }

        [Fact]
        public void TryLockpick_Fails_At_Range()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var pick = new Item { EntityId = "pick1" };
            pick.Set(new Lockpick { SkillLevel = 10, Durability = 10 });
            session.Player!.Get<Inventory>()!.TryAdd(pick.EntityId, pick);
            var door = PlaceDoor(session, new WorldLocation(15, 5, 0), locked: true);

            var result = new InteractionSystem().TryLockpick(session, pick.EntityId, door.EntityId);

            Assert.False(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsLocked);
        }

        [Fact]
        public void TryForceOpen_Fails_At_Range()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var crowbar = new Item { EntityId = "crowbar1" };
            crowbar.Set(new ForcesDoor { Strength = 5, Durability = 10 });
            session.Player!.Get<Inventory>()!.TryAdd(crowbar.EntityId, crowbar);
            var door = PlaceDoor(session, new WorldLocation(15, 5, 0), locked: true);

            var result = new InteractionSystem().TryForceOpen(session, crowbar.EntityId, door.EntityId);

            Assert.False(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsLocked);
        }

        [Fact]
        public void TryActivate_Fails_Across_Z_Levels()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var lever = new Item { EntityId = "lever1" };
            lever.Set(new WorldLocation(15, 15, 1)); // same X/Y, one floor up
            lever.Set(new Activatable { ToggleBehavior = true });
            session.World.AddEntity(lever);

            var result = new InteractionSystem().TryActivate(session, lever.EntityId);

            Assert.False(result.Success);
            Assert.Contains("far", result.Reason.ToLowerInvariant());
        }

        [Fact]
        public void Adjacent_Lever_Still_Opens_Remote_Door()
        {
            var session = CreateSession();
            session.ViewLocation = new WorldLocation(15, 15, 0);

            // Lever mechanisms legitimately act at a distance via TargetEntityIds;
            // only the lever itself must be within reach.
            var door = PlaceDoor(session, new WorldLocation(28, 15, 0), locked: true);
            var lever = new Item { EntityId = "lever1" };
            lever.Set(new WorldLocation(16, 15, 0)); // adjacent to actor
            lever.Set(new Activatable { ToggleBehavior = false, TargetEntityIds = { door.EntityId } });
            session.World.AddEntity(lever);

            var result = new InteractionSystem().TryActivate(session, lever.EntityId);

            Assert.True(result.Success);
            Assert.False(door.Get<OpensAndCloses>().IsLocked);
            Assert.True(door.Get<OpensAndCloses>().IsOpen);
        }
    }
}
