using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Xunit;

namespace Aetherium.Test
{
    public class InventoryTests
    {
        [Fact]
        public void Inventory_AddItem_WithinCapacity()
        {
            // Arrange
            var inventory = new Inventory { Capacity = 10 };
            var item = new Item { EntityId = "item1" };

            // Act
            var result = inventory.TryAdd("item1", item);

            // Assert
            Assert.True(result);
            Assert.Single(inventory.ItemEntityIds);
            Assert.Equal("item1", inventory.ItemEntityIds.First());
            Assert.True(inventory.Items.ContainsKey("item1"));
        }

        [Fact]
        public void Inventory_AddItem_ExceedsCapacity()
        {
            // Arrange
            var inventory = new Inventory { Capacity = 2 };
            var item1 = new Item { EntityId = "item1" };
            var item2 = new Item { EntityId = "item2" };
            var item3 = new Item { EntityId = "item3" };

            // Act
            inventory.TryAdd("item1", item1);
            inventory.TryAdd("item2", item2);
            var result = inventory.TryAdd("item3", item3);

            // Assert
            Assert.False(result);
            Assert.Equal(2, inventory.ItemEntityIds.Count);
            Assert.False(inventory.Items.ContainsKey("item3"));
        }

        [Fact]
        public void Inventory_RemoveItem_Success()
        {
            // Arrange
            var inventory = new Inventory { Capacity = 10 };
            var item = new Item { EntityId = "item1" };
            inventory.TryAdd("item1", item);

            // Act
            var result = inventory.Remove("item1");

            // Assert
            Assert.True(result);
            Assert.Empty(inventory.ItemEntityIds);
            Assert.False(inventory.Items.ContainsKey("item1"));
        }

        [Fact]
        public void Inventory_RemoveItem_NotFound()
        {
            // Arrange
            var inventory = new Inventory { Capacity = 10 };

            // Act
            var result = inventory.Remove("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Inventory_CharacterHasInventory_OnCreation()
        {
            // Arrange & Act
            var character = new Character();
            character.Set(new Inventory());

            // Assert
            Assert.True(character.Has<Inventory>());
            var inventory = character.Get<Inventory>();
            Assert.NotNull(inventory);
            Assert.Equal(10, inventory.Capacity); // default capacity
        }
    }

    public class KeyDoorTests
    {
        [Fact]
        public void KeyItem_HasKeyComponent()
        {
            // Arrange & Act
            var key = new KeyItem("red");

            // Assert
            Assert.True(key.Has<Key>());
            var keyComp = key.Get<Key>();
            Assert.Equal("red", keyComp.KeyId);
        }

        [Fact]
        public void KeyItem_IsCarriable()
        {
            // Arrange & Act
            var key = new KeyItem("blue");

            // Assert
            Assert.True(key.Has<Carriable>());
            var carriable = key.Get<Carriable>();
            Assert.NotNull(carriable);
        }

        [Fact]
        public void Door_HasOpensAndCloses()
        {
            // Arrange & Act
            var door = new Door();

            // Assert
            Assert.True(door.Has<OpensAndCloses>());
            var opens = door.Get<OpensAndCloses>();
            Assert.False(opens.IsOpen);
            Assert.False(opens.IsLocked);
        }

        [Fact]
        public void Door_Closed_ObstructsView()
        {
            // Arrange
            var door = new Door();
            door.Get<OpensAndCloses>().IsOpen = false;

            // Assert
            Assert.True(door.Has<ObstructsView>());
        }

        [Fact]
        public void Key_MatchesDoor_ByKeyShape()
        {
            // Arrange
            var key = new KeyItem("red");
            var door = new Door();
            door.Get<OpensAndCloses>().KeyShape = "red";
            door.Get<OpensAndCloses>().IsLocked = true;

            // Act - Check if key matches
            var keyComp = key.Get<Key>();
            var doorComp = door.Get<OpensAndCloses>();
            var matches = !string.IsNullOrEmpty(doorComp.KeyShape) && doorComp.KeyShape == keyComp.KeyId;

            // Assert
            Assert.True(matches);
        }

        [Fact]
        public void Key_DoesNotMatchDoor_DifferentKeyShape()
        {
            // Arrange
            var key = new KeyItem("red");
            var door = new Door();
            door.Get<OpensAndCloses>().KeyShape = "blue";

            // Act
            var keyComp = key.Get<Key>();
            var doorComp = door.Get<OpensAndCloses>();
            var matches = !string.IsNullOrEmpty(doorComp.KeyShape) && doorComp.KeyShape == keyComp.KeyId;

            // Assert
            Assert.False(matches);
        }
    }
}

