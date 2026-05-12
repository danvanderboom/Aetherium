using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies GameSession.ApplyDelta semantics for each delta type. These run
    /// purely in-process (no Orleans) by constructing sessions directly and
    /// calling ApplyDelta on them.
    /// </summary>
    public class DeltaApplicationTests
    {
        private static GameSession NewSession() =>
            new GameSession("delta-test", new FovDiagnosticWorldBuilder("open_space"));

        [Fact]
        public void EntityMovedDelta_Updates_Mirror_Position()
        {
            var session = NewSession();
            var item = new KeyItem("test");
            item.Set(new WorldLocation(5, 5, 0));
            session.World.AddEntity(item);

            session.ApplyDelta(new EntityMovedDelta
            {
                EntityId = item.EntityId,
                OldX = 5, OldY = 5, OldZ = 0,
                NewX = 7, NewY = 8, NewZ = 0,
            });

            Assert.Equal(7, item.Get<WorldLocation>().X);
            Assert.Equal(8, item.Get<WorldLocation>().Y);
        }

        [Fact]
        public void EntityRemovedDelta_Removes_From_Mirror()
        {
            var session = NewSession();
            var item = new KeyItem("removable");
            item.Set(new WorldLocation(3, 3, 0));
            session.World.AddEntity(item);

            session.ApplyDelta(new EntityRemovedDelta { EntityId = item.EntityId });

            Assert.False(session.World.Entities.ContainsKey(item.EntityId));
        }

        [Fact]
        public void EntityRemovedDelta_For_Unknown_Id_Is_Silently_Ignored()
        {
            var session = NewSession();
            // No exception should be thrown.
            session.ApplyDelta(new EntityRemovedDelta { EntityId = "never-existed" });
            Assert.True(true);
        }

        [Fact]
        public void EntityAddedDelta_Creates_Entity_In_Mirror()
        {
            var session = NewSession();
            session.ApplyDelta(new EntityAddedDelta
            {
                Placement = new EntityPlacement
                {
                    EntityId = "added-key",
                    TypeName = nameof(KeyItem),
                    X = 4, Y = 4, Z = 0,
                    Properties = new Dictionary<string, string> { ["KeyId"] = "red" },
                },
            });

            Assert.True(session.World.Entities.TryGetValue("added-key", out var entity));
            Assert.IsType<KeyItem>(entity);
        }

        [Fact]
        public void DoorStateChangedDelta_Toggles_OpensAndCloses()
        {
            var session = NewSession();
            var door = new Door();
            door.Set(new WorldLocation(1, 1, 0));
            session.World.AddEntity(door);
            var oc = door.Get<OpensAndCloses>();
            Assert.NotNull(oc);
            Assert.False(oc!.IsOpen);

            session.ApplyDelta(new DoorStateChangedDelta
            {
                EntityId = door.EntityId,
                IsOpen = true,
                IsLocked = false,
            });

            Assert.True(oc.IsOpen);
            Assert.False(oc.IsLocked);
        }

        [Fact]
        public void ItemTransferredDelta_Inventory_To_World_Drops_Item()
        {
            var session = NewSession();
            // Manually place an item in the player's inventory.
            var item = new KeyItem("dropper");
            session.Player!.Get<Inventory>()!.TryAdd(item.EntityId, item);

            session.ApplyDelta(new ItemTransferredDelta
            {
                ItemEntityId = item.EntityId,
                IntoInventory = false,
                OwnerEntityId = session.Player.EntityId,
                X = 2, Y = 2, Z = 0,
            });

            Assert.False(session.Player.Get<Inventory>()!.Items.ContainsKey(item.EntityId));
            Assert.True(session.World.Entities.ContainsKey(item.EntityId));
        }

        [Fact]
        public void ItemTransferredDelta_World_To_Inventory_Adds_To_Owner()
        {
            var session = NewSession();
            var ownerId = session.Player!.EntityId;
            // The item is created from the placement during ApplyDelta.
            session.ApplyDelta(new ItemTransferredDelta
            {
                ItemEntityId = "picked-key",
                IntoInventory = true,
                OwnerEntityId = ownerId,
                X = 1, Y = 1, Z = 0,
                ItemPlacement = new EntityPlacement
                {
                    EntityId = "picked-key",
                    TypeName = nameof(KeyItem),
                    X = 1, Y = 1, Z = 0,
                    Properties = new Dictionary<string, string> { ["KeyId"] = "blue" },
                },
            });

            Assert.True(session.Player.Get<Inventory>()!.Items.ContainsKey("picked-key"));
        }

        [Fact]
        public void EntityHeadingChangedDelta_Updates_Character_HasHeading()
        {
            var session = NewSession();
            Assert.Equal(0, session.Player!.Get<HasHeading>()!.Heading);

            session.ApplyDelta(new EntityHeadingChangedDelta
            {
                EntityId = session.Player.EntityId,
                Degrees = 270,
            });

            Assert.Equal(270, session.Player.Get<HasHeading>()!.Heading);
            // GameSession.HeadingDegrees reads through to Player's HasHeading.
            Assert.Equal(270, session.HeadingDegrees);
        }

        [Fact]
        public void Null_Delta_Is_Safe()
        {
            var session = NewSession();
            session.ApplyDelta(null!);
            Assert.True(true);
        }

        [Fact]
        public void Unknown_Delta_Type_Is_Logged_And_Dropped()
        {
            // Construct a custom subtype the dispatcher doesn't know about. Since the
            // hierarchy is sealed at the runtime types we already defined, the test
            // verifies the default branch by checking no exception throws even when
            // unknown subclasses appear (would only happen across version mismatches).
            var session = NewSession();
            session.ApplyDelta(new UnknownDelta());
            Assert.True(true);
        }

        private class UnknownDelta : MapDelta { }
    }
}
