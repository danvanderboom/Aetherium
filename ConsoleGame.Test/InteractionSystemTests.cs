using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.WorldBuilders;
using ConsoleGameServer;
using Xunit;

namespace ConsoleGame.Test
{
    public class InteractionSystemTests
    {
        [Fact]
        public void InteractionSystem_Pickup_Success()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var item = new Item { EntityId = "item1" };
            item.Set(new WorldLocation(15, 15, 0));
            item.Set(new Carriable { Label = "Test Item", Icon = "*" });
            session.World.AddEntity(item);

            // Act
            var system = new InteractionSystem();
            var result = system.TryPickup(session, "item1");

            // Assert
            Assert.True(result.Success);
            Assert.True(session.Player.Get<Inventory>().ItemEntityIds.Contains("item1"));
            Assert.False(session.World.Entities.ContainsKey("item1"));
        }

        [Fact]
        public void InteractionSystem_Pickup_NotAtSameLocation()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var item = new Item { EntityId = "item1" };
            item.Set(new WorldLocation(16, 16, 0)); // Different location
            item.Set(new Carriable());
            session.World.AddEntity(item);

            // Act
            var system = new InteractionSystem();
            var result = system.TryPickup(session, "item1");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Not at same location", result.Reason);
        }

        [Fact]
        public void InteractionSystem_Pickup_InventoryFull()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var inventory = session.Player.Get<Inventory>();
            inventory.Capacity = 1;
            
            // Fill inventory
            var item1 = new Item { EntityId = "item1" };
            inventory.TryAdd("item1", item1);

            var item2 = new Item { EntityId = "item2" };
            item2.Set(new WorldLocation(15, 15, 0));
            item2.Set(new Carriable());
            session.World.AddEntity(item2);

            // Act
            var system = new InteractionSystem();
            var result = system.TryPickup(session, "item2");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Inventory full", result.Reason);
        }

        [Fact]
        public void InteractionSystem_Drop_Success()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            session.ViewLocation = new WorldLocation(15, 15, 0);
            var item = new Item { EntityId = "item1" };
            item.Set(new Carriable());
            session.Player.Get<Inventory>().TryAdd("item1", item);

            // Act
            var system = new InteractionSystem();
            var result = system.TryDrop(session, "item1");

            // Assert
            Assert.True(result.Success);
            Assert.False(session.Player.Get<Inventory>().ItemEntityIds.Contains("item1"));
            Assert.True(session.World.Entities.ContainsKey("item1"));
            Assert.Equal(session.ViewLocation, session.World.Entities["item1"].Get<WorldLocation>());
        }

        [Fact]
        public void InteractionSystem_UseKey_UnlocksDoor()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var key = new KeyItem("red");
            session.Player.Get<Inventory>().TryAdd(key.EntityId, key);

            var door = new Door();
            door.Set(new WorldLocation(16, 15, 0));
            door.Get<OpensAndCloses>().KeyShape = "red";
            door.Get<OpensAndCloses>().IsLocked = true;
            session.World.AddEntity(door);

            // Act
            var system = new InteractionSystem();
            var result = system.TryUse(session, key.EntityId, door.EntityId);

            // Assert
            Assert.True(result.Success);
            Assert.False(door.Get<OpensAndCloses>().IsLocked);
        }

        [Fact]
        public void InteractionSystem_UseKey_WrongKey()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var key = new KeyItem("red");
            session.Player.Get<Inventory>().TryAdd(key.EntityId, key);

            var door = new Door();
            door.Set(new WorldLocation(16, 15, 0));
            door.Get<OpensAndCloses>().KeyShape = "blue"; // Different key
            door.Get<OpensAndCloses>().IsLocked = true;
            session.World.AddEntity(door);

            // Act
            var system = new InteractionSystem();
            var result = system.TryUse(session, key.EntityId, door.EntityId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Key does not match", result.Reason);
            Assert.True(door.Get<OpensAndCloses>().IsLocked);
        }

        [Fact]
        public void InteractionSystem_OpenDoor_Success()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var door = new Door();
            door.Set(new WorldLocation(16, 15, 0));
            door.Get<OpensAndCloses>().IsOpen = false;
            door.Get<OpensAndCloses>().IsLocked = false;
            session.World.AddEntity(door);

            // Act
            var system = new InteractionSystem();
            var result = system.TryOpen(session, door.EntityId);

            // Assert
            Assert.True(result.Success);
            Assert.True(door.Get<OpensAndCloses>().IsOpen);
            Assert.False(door.Has<ObstructsView>());
        }

        [Fact]
        public void InteractionSystem_OpenDoor_Locked()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var door = new Door();
            door.Set(new WorldLocation(16, 15, 0));
            door.Get<OpensAndCloses>().IsLocked = true;
            session.World.AddEntity(door);

            // Act
            var system = new InteractionSystem();
            var result = system.TryOpen(session, door.EntityId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Locked", result.Reason);
        }

        [Fact]
        public void InteractionSystem_CloseDoor_Success()
        {
            // Arrange
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var door = new Door();
            door.Set(new WorldLocation(16, 15, 0));
            door.Get<OpensAndCloses>().IsOpen = true;
            door.Get<OpensAndCloses>().IsLocked = false;
            door.Clear<ObstructsView>();
            session.World.AddEntity(door);

            // Act
            var system = new InteractionSystem();
            var result = system.TryClose(session, door.EntityId);

            // Assert
            Assert.True(result.Success);
            Assert.False(door.Get<OpensAndCloses>().IsOpen);
            Assert.True(door.Has<ObstructsView>());
        }
    }
}

