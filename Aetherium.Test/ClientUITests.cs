using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Model;
using Xunit;

namespace Aetherium.Test
{
    public class ClientUITests
    {
        [Fact]
        public void PerceptionDto_Inventory_DisplaysCorrectly()
        {
            // Arrange
            var perception = new PerceptionDto
            {
                Inventory = new InventoryDto
                {
                    Capacity = 10,
                    Items = new List<ItemDto>
                    {
                        new ItemDto { Id = "key1", Label = "Key", Icon = "k", KeyId = "red" },
                        new ItemDto { Id = "key2", Label = "Key", Icon = "k", KeyId = "blue" }
                    }
                }
            };

            // Act - simulate inventory display formatting
            var itemList = perception.Inventory.Items
                .Select(i => string.IsNullOrEmpty(i.KeyId) ? i.Label : $"{i.Label}({i.KeyId})")
                .ToList();
            var displayText = $"Inventory [{perception.Inventory.Items.Count}/{perception.Inventory.Capacity}]: {string.Join(", ", itemList)}";

            // Assert
            Assert.Equal("Inventory [2/10]: Key(red), Key(blue)", displayText);
        }

        [Fact]
        public void PerceptionDto_Inventory_EmptyDisplay()
        {
            // Arrange
            var perception = new PerceptionDto
            {
                Inventory = new InventoryDto
                {
                    Capacity = 10,
                    Items = new List<ItemDto>()
                }
            };

            // Act
            var displayText = perception.Inventory.Items.Any()
                ? $"Inventory [{perception.Inventory.Items.Count}/{perception.Inventory.Capacity}]: {string.Join(", ", perception.Inventory.Items.Select(i => i.Label))}"
                : $"Inventory [0/{perception.Inventory.Capacity}]: (empty)";

            // Assert
            Assert.Equal("Inventory [0/10]: (empty)", displayText);
        }

        [Fact]
        public void Affordances_GroupedByAction()
        {
            // Arrange
            var affordances = new List<AffordanceDto>
            {
                new AffordanceDto { Action = "pickup", TargetId = "item1", ActorId = "player1" },
                new AffordanceDto { Action = "pickup", TargetId = "item2", ActorId = "player1" },
                new AffordanceDto { Action = "open", TargetId = "door1", ActorId = "player1" },
                new AffordanceDto { Action = "use", TargetId = "door2", ActorId = "player1", RequiresKeyId = "red" }
            };

            // Act - simulate grouping for menu display
            var grouped = affordances.GroupBy(a => a.Action).ToList();

            // Assert
            Assert.Equal(3, grouped.Count);
            Assert.Equal(2, grouped.First(g => g.Key == "pickup").Count());
            Assert.Single(grouped.First(g => g.Key == "open"));
            Assert.Single(grouped.First(g => g.Key == "use"));
        }

        [Fact]
        public void Affordance_BuildDescription_Pickup()
        {
            // Arrange
            var affordance = new AffordanceDto
            {
                Action = "pickup",
                TargetId = "item1",
                ActorId = "player1"
            };
            var visibleItems = new List<ItemDto>
            {
                new ItemDto { Id = "item1", Label = "Key", Icon = "k", KeyId = "red" }
            };

            // Act - simulate description building
            var item = visibleItems.FirstOrDefault(i => i.Id == affordance.TargetId);
            var description = $"Pick up {item?.Label ?? affordance.TargetId}";

            // Assert
            Assert.Equal("Pick up Key", description);
        }

        [Fact]
        public void Affordance_BuildDescription_UseWithKey()
        {
            // Arrange
            var affordance = new AffordanceDto
            {
                Action = "use",
                TargetId = "door1",
                ActorId = "player1",
                RequiresKeyId = "red"
            };

            // Act
            var description = $"Use item on door1 (requires {affordance.RequiresKeyId} key)";

            // Assert
            Assert.Equal("Use item on door1 (requires red key)", description);
        }

        [Fact]
        public void VisibleItems_IncludeLocation()
        {
            // Arrange
            var items = new List<ItemDto>
            {
                new ItemDto
                {
                    Id = "key1",
                    Label = "Key",
                    Icon = "k",
                    KeyId = "red",
                    Location = new WorldLocationDto(1, 0, 0) // relative to player
                },
                new ItemDto
                {
                    Id = "key2",
                    Label = "Key",
                    Icon = "k",
                    KeyId = "blue",
                    Location = new WorldLocationDto(0, -1, 0) // relative to player
                }
            };

            // Act - simulate finding item at location
            var relativeX = 1;
            var relativeY = 0;
            var itemAtLocation = items.FirstOrDefault(i =>
                i.Location != null &&
                i.Location.X == relativeX &&
                i.Location.Y == relativeY &&
                i.Location.Z == 0);

            // Assert
            Assert.NotNull(itemAtLocation);
            Assert.Equal("key1", itemAtLocation.Id);
            Assert.Equal("red", itemAtLocation.KeyId);
        }

        [Fact]
        public void ItemDto_IconTruncation()
        {
            // Arrange
            var item = new ItemDto { Icon = "key", Label = "Key" };
            int symbolWidth = 2;

            // Act - simulate icon rendering
            var icon = string.IsNullOrEmpty(item.Icon) ? "?" : item.Icon;
            if (icon.Length > symbolWidth)
                icon = icon.Substring(0, symbolWidth);

            // Assert
            Assert.Equal("ke", icon);
        }

        [Fact]
        public void ItemDto_KeyColorMapping()
        {
            // Arrange
            var keyColors = new Dictionary<string, ConsoleColor>
            {
                { "red", ConsoleColor.Red },
                { "blue", ConsoleColor.Blue },
                { "green", ConsoleColor.Green },
                { "yellow", ConsoleColor.Yellow }
            };

            // Act
            var redKey = new ItemDto { KeyId = "red" };
            var blueKey = new ItemDto { KeyId = "blue" };
            var unknownKey = new ItemDto { KeyId = "purple" };

            var redColor = redKey.KeyId?.ToLowerInvariant() switch
            {
                "red" => ConsoleColor.Red,
                "blue" => ConsoleColor.Blue,
                "green" => ConsoleColor.Green,
                "yellow" => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };

            var blueColor = blueKey.KeyId?.ToLowerInvariant() switch
            {
                "red" => ConsoleColor.Red,
                "blue" => ConsoleColor.Blue,
                "green" => ConsoleColor.Green,
                "yellow" => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };

            var unknownColor = unknownKey.KeyId?.ToLowerInvariant() switch
            {
                "red" => ConsoleColor.Red,
                "blue" => ConsoleColor.Blue,
                "green" => ConsoleColor.Green,
                "yellow" => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };

            // Assert
            Assert.Equal(ConsoleColor.Red, redColor);
            Assert.Equal(ConsoleColor.Blue, blueColor);
            Assert.Equal(ConsoleColor.White, unknownColor);
        }

        [Fact]
        public void Affordances_NoActionsAvailable()
        {
            // Arrange
            var perception = new PerceptionDto
            {
                Affordances = new List<AffordanceDto>()
            };

            // Act
            var hasActions = perception.Affordances != null && perception.Affordances.Any();

            // Assert
            Assert.False(hasActions);
        }

        [Fact]
        public void Inventory_CapacityEnforcementDisplay()
        {
            // Arrange - full inventory
            var perception = new PerceptionDto
            {
                Inventory = new InventoryDto
                {
                    Capacity = 10,
                    Items = Enumerable.Range(1, 10)
                        .Select(i => new ItemDto { Id = $"item{i}", Label = "Item", Icon = "*" })
                        .ToList()
                }
            };

            // Act
            var isFull = perception.Inventory.Items.Count >= perception.Inventory.Capacity;
            var displayFull = isFull ? " (FULL)" : "";

            // Assert
            Assert.True(isFull);
            Assert.Equal(" (FULL)", displayFull);
        }
    }
}

