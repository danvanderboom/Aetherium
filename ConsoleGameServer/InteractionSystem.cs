using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGameServer
{
    public class InteractionResult
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = string.Empty;
        public static InteractionResult Ok() => new InteractionResult { Success = true };
        public static InteractionResult Fail(string reason) => new InteractionResult { Success = false, Reason = reason };
    }

    public class InteractionSystem
    {
        public InteractionResult TryPickup(GameSession session, string targetEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");

            var world = session.World;
            if (!world.Entities.TryGetValue(targetEntityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var carriable = target.AllComponents.OfType<Carriable>().FirstOrDefault();
            if (carriable == null)
                return InteractionResult.Fail("Not carriable");

            if (target.Get<WorldLocation>() != session.ViewLocation)
                return InteractionResult.Fail("Not at same location");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.TryAdd(target.EntityId, target))
                return InteractionResult.Fail("Inventory full");

            world.RemoveEntity(target.EntityId);
            return InteractionResult.Ok();
        }

        public InteractionResult TryDrop(GameSession session, string itemEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemEntityId, out var entity))
                return InteractionResult.Fail("Item not in inventory");

            var world = session.World;

            // Place entity at player's location
            entity.Set(new WorldLocation(session.ViewLocation.X, session.ViewLocation.Y, session.ViewLocation.Z));
            world.AddEntity(entity);

            inventory.Remove(itemEntityId);
            return InteractionResult.Ok();
        }

        public InteractionResult TryOpen(GameSession session, string targetEntityId)
        {
            var result = ToggleDoor(session, targetEntityId, open: true);
            return result;
        }

        public InteractionResult TryClose(GameSession session, string targetEntityId)
        {
            var result = ToggleDoor(session, targetEntityId, open: false);
            return result;
        }

        public InteractionResult TryUse(GameSession session, string itemEntityId, string onEntityId)
        {
            if (session.Player == null)
                return InteractionResult.Fail("No player");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemEntityId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!session.World.Entities.TryGetValue(onEntityId, out var target))
                return InteractionResult.Fail("Target not found");

            // Key on a door: match Key.KeyId to OpensAndCloses.KeyShape
            var key = item.AllComponents.OfType<Key>().FirstOrDefault();
            var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (key != null && door != null)
            {
                if (!string.IsNullOrEmpty(door.KeyShape) && door.KeyShape == key.KeyId)
                {
                    door.IsLocked = false;
                    return InteractionResult.Ok();
                }
                return InteractionResult.Fail("Key does not match");
            }

            return InteractionResult.Fail("No effect");
        }

        private InteractionResult ToggleDoor(GameSession session, string targetEntityId, bool open)
        {
            if (!session.World.Entities.TryGetValue(targetEntityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (door == null)
                return InteractionResult.Fail("Not a door");

            if (door.IsLocked)
                return InteractionResult.Fail("Locked");

            door.IsOpen = open;
            var world = session.World;
            if (open)
            {
                target.Clear<ObstructsView>();
                var tile = target.Get<Tile>();
                if (tile != null && world.TileTypes.ContainsKey("Indoors"))
                    tile.Type = world.TileTypes["Indoors"];
            }
            else
            {
                target.Set(new ObstructsView { Opacity = 1 });
                var tile = target.Get<Tile>();
                if (tile != null && world.TileTypes.ContainsKey("Wall"))
                    tile.Type = world.TileTypes["Wall"];
            }
            return InteractionResult.Ok();
        }

        public InteractionResult TryActivate(GameSession session, string entityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");

            if (!session.World.Entities.TryGetValue(entityId, out var target))
                return InteractionResult.Fail("Entity not found");

            // Check if entity is adjacent or at same location
            var targetLoc = target.Get<WorldLocation>();
            var distance = System.Math.Abs(targetLoc.X - session.ViewLocation.X) + 
                          System.Math.Abs(targetLoc.Y - session.ViewLocation.Y);
            if (distance > 1)
                return InteractionResult.Fail("Too far away");

            var activatable = target.AllComponents.OfType<Activatable>().FirstOrDefault();
            if (activatable == null)
                return InteractionResult.Fail("Not activatable");

            // Toggle or activate
            if (activatable.ToggleBehavior)
            {
                activatable.IsActivated = !activatable.IsActivated;
            }
            else
            {
                activatable.IsActivated = true;
            }

            // Activate target entities (doors, mechanisms, etc.)
            foreach (var targetId in activatable.TargetEntityIds)
            {
                if (session.World.Entities.TryGetValue(targetId, out var targetEntity))
                {
                    // If target is a door, unlock/open it
                    var door = targetEntity.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                    if (door != null && activatable.IsActivated)
                    {
                        door.IsLocked = false;
                        if (!door.IsOpen)
                        {
                            ToggleDoor(session, targetId, open: true);
                        }
                    }

                    // If target has LightSource, toggle it based on activation
                    var lightSource = targetEntity.AllComponents.OfType<LightSource>().FirstOrDefault();
                    if (lightSource != null)
                    {
                        lightSource.IsEnabled = activatable.IsActivated;
                    }
                }
            }

            return InteractionResult.Ok();
        }

        public InteractionResult TryConsume(GameSession session, string itemId)
        {
            if (session.Player == null)
                return InteractionResult.Fail("No player");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            var consumable = item.AllComponents.OfType<Consumable>().FirstOrDefault();
            if (consumable == null)
                return InteractionResult.Fail("Not consumable");

            if (consumable.Uses <= 0)
                return InteractionResult.Fail("No uses remaining");

            // Apply effect
            var health = session.Player.Get<Health>();
            if (health != null)
            {
                switch (consumable.EffectType)
                {
                    case ConsumableEffectType.HealthRestore:
                        health.Level = System.Math.Min(health.MaxLevel, health.Level + consumable.EffectValue);
                        break;
                    // Other effect types can be added later
                }
            }

            // Decrease uses
            consumable.Uses--;
            if (consumable.Uses <= 0)
            {
                // Remove from inventory when fully consumed
                inventory.Remove(itemId);
            }

            return InteractionResult.Ok();
        }

        public InteractionResult TryPlace(GameSession session, string itemId, WorldLocation? location = null)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            var placeLocation = location ?? session.ViewLocation;

            // Check if item can be placed (has PlaceableLight component)
            var placeableLight = item.AllComponents.OfType<PlaceableLight>().FirstOrDefault();
            if (placeableLight != null)
            {
                placeableLight.IsPlaced = true;
                var lightSource = item.AllComponents.OfType<LightSource>().FirstOrDefault();
                if (lightSource != null)
                {
                    lightSource.IsDynamic = false;
                    lightSource.IsEnabled = true;
                }
            }
            else
            {
                return InteractionResult.Fail("Item cannot be placed");
            }

            // Remove from inventory and add to world
            inventory.Remove(itemId);
            item.Set(new WorldLocation(placeLocation.X, placeLocation.Y, placeLocation.Z));
            session.World.AddEntity(item);

            return InteractionResult.Ok();
        }

        public InteractionResult TryClimb(GameSession session, string entityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");

            if (!session.World.Entities.TryGetValue(entityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var climbable = target.AllComponents.OfType<Climbable>().FirstOrDefault();
            if (climbable == null)
                return InteractionResult.Fail("Not climbable");

            if (climbable.RequiresItem && !string.IsNullOrEmpty(climbable.RequiredItemId))
            {
                var inventory = session.Player.Get<Inventory>();
                if (inventory == null || !inventory.Items.ContainsKey(climbable.RequiredItemId))
                    return InteractionResult.Fail("Required item not in inventory");
            }

            // Climbing logic - for now, just verify it's at the same location
            var targetLoc = target.Get<WorldLocation>();
            if (targetLoc != session.ViewLocation)
                return InteractionResult.Fail("Not at climbable location");

            // Climbing would typically change Z level, but that's handled by movement system
            // This interaction just verifies the climb can be attempted
            return InteractionResult.Ok();
        }

        public InteractionResult TryForceOpen(GameSession session, string itemId, string doorId)
        {
            if (session.Player == null)
                return InteractionResult.Fail("No player");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!session.World.Entities.TryGetValue(doorId, out var door))
                return InteractionResult.Fail("Door not found");

            var forcesDoor = item.AllComponents.OfType<ForcesDoor>().FirstOrDefault();
            if (forcesDoor == null)
                return InteractionResult.Fail("Item cannot force doors");

            var doorComp = door.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (doorComp == null)
                return InteractionResult.Fail("Target is not a door");

            // Check if crowbar is strong enough (simplified: always works if unlocked or strength > 0)
            if (forcesDoor.Strength > 0)
            {
                doorComp.IsLocked = false;
                if (!doorComp.IsOpen)
                {
                    ToggleDoor(session, doorId, open: true);
                }

                // Reduce durability
                forcesDoor.Durability--;
                if (forcesDoor.Durability <= 0)
                {
                    // Tool breaks
                    inventory.Remove(itemId);
                    return InteractionResult.Ok();
                }
            }

            return InteractionResult.Ok();
        }

        public InteractionResult TryLockpick(GameSession session, string itemId, string doorId)
        {
            if (session.Player == null)
                return InteractionResult.Fail("No player");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!session.World.Entities.TryGetValue(doorId, out var door))
                return InteractionResult.Fail("Door not found");

            var lockpick = item.AllComponents.OfType<Lockpick>().FirstOrDefault();
            if (lockpick == null)
                return InteractionResult.Fail("Item is not a lockpick");

            var doorComp = door.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (doorComp == null || !doorComp.IsLocked)
                return InteractionResult.Fail("Door is not locked");

            // Simplified lockpicking: success based on skill level (60% + 5% per skill level)
            var successChance = 0.6 + (lockpick.SkillLevel * 0.05);
            var random = new System.Random();
            var roll = random.NextDouble();

            if (roll < successChance)
            {
                doorComp.IsLocked = false;
            }
            else
            {
                // Reduce durability on failure
                lockpick.Durability--;
                if (lockpick.Durability <= 0)
                {
                    inventory.Remove(itemId);
                    return InteractionResult.Fail("Lockpick broke");
                }
                return InteractionResult.Fail("Lockpicking failed");
            }

            // Reduce durability on success too
            lockpick.Durability--;
            if (lockpick.Durability <= 0)
            {
                inventory.Remove(itemId);
            }

            return InteractionResult.Ok();
        }

        public InteractionResult TryEquip(GameSession session, string itemId)
        {
            if (session.Player == null)
                return InteractionResult.Fail("No player");

            var inventory = session.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            // Equip backpack (increases capacity)
            var capacityBoost = item.AllComponents.OfType<CapacityBoost>().FirstOrDefault();
            if (capacityBoost != null)
            {
                // Increase inventory capacity
                inventory.Capacity += capacityBoost.AdditionalCapacity;
                return InteractionResult.Ok();
            }

            // Equip concealment cloak (affects perception - handled by perception system)
            var hidden = item.AllComponents.OfType<Hidden>().FirstOrDefault();
            if (hidden != null)
            {
                // Cloak is equipped - perception system will check this
                // For now, just mark as equipped by storing on player
                session.Player.Set(item);
                return InteractionResult.Ok();
            }

            return InteractionResult.Fail("Item cannot be equipped");
        }
    }
}


