using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.Server
{
    /// <summary>
    /// Minimum context needed to apply any gameplay verb to a world. The same
    /// three components that <see cref="GameSession"/> exposes (World, the actor
    /// Character, the actor's current ViewLocation), packaged so non-session
    /// callers (e.g. <c>IGameMapGrain</c>) can drive <see cref="InteractionSystem"/>
    /// without owning a full session.
    ///
    /// <para>
    /// Every public <c>InteractionSystem.Try*</c> method has an overload that
    /// takes this record. The legacy session-taking overloads are thin
    /// forwarders. Phase 2d's deferred refactor — completed here.
    /// </para>
    /// </summary>
    public sealed record class ActionContext(World World, Character Player, WorldLocation ViewLocation);

    public class InteractionResult
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<UseOption>? Options { get; set; } // For reactive disambiguation
        
        public static InteractionResult Ok() => new InteractionResult { Success = true };
        public static InteractionResult Fail(string reason) => new InteractionResult { Success = false, Reason = reason };
        public static InteractionResult OptionsResult(List<UseOption> options) => new InteractionResult 
        { 
            Success = false, 
            Reason = "Multiple usage options available", 
            Options = options 
        };
    }

    /// <summary>
    /// Represents a single usage option for an item.
    /// </summary>
    public class UseOption
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ContextRequirements { get; set; } = new List<string>();
    }

    public class InteractionSystem
    {
        private readonly IRandomSource _random;

        public InteractionSystem(IRandomSource? random = null)
        {
            _random = random ?? new DefaultRandomSource();
        }

        // Session-taking overloads (legacy / LocalMutationGateway callers) forward
        // to the ActionContext canonical implementations below.
        public InteractionResult TryPickup(GameSession session, string targetEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryPickup(new ActionContext(session.World, session.Player, session.ViewLocation), targetEntityId);
        }

        public InteractionResult TryDrop(GameSession session, string itemEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryDrop(new ActionContext(session.World, session.Player, session.ViewLocation), itemEntityId);
        }

        public InteractionResult TryOpen(GameSession session, string targetEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryOpen(new ActionContext(session.World, session.Player, session.ViewLocation), targetEntityId);
        }

        public InteractionResult TryClose(GameSession session, string targetEntityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryClose(new ActionContext(session.World, session.Player, session.ViewLocation), targetEntityId);
        }

        // ============================================================
        // ActionContext canonical implementations — grain-callable.
        // ============================================================

        public InteractionResult TryPickup(ActionContext ctx, string targetEntityId)
        {
            if (!ctx.World.Entities.TryGetValue(targetEntityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var carriable = target.AllComponents.OfType<Carriable>().FirstOrDefault();
            if (carriable == null)
                return InteractionResult.Fail("Not carriable");

            if (target.Get<WorldLocation>() != ctx.ViewLocation)
                return InteractionResult.Fail("Not at same location");

            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            // Reserve via atomic remove (closes the TOCTOU window between two
            // concurrent pickups of the same item).
            if (!ctx.World.TryRemoveEntity(target.EntityId))
                return InteractionResult.Fail("Already picked up");

            if (!inventory.TryAdd(target.EntityId, target))
            {
                ctx.World.AddEntity(target); // rollback
                return InteractionResult.Fail("Inventory full");
            }

            ctx.World.EmitEvent(new WorldEvent
            {
                EventType = WorldEventType.ItemPickedUp,
                Location = ctx.ViewLocation,
                Entity = target
            });

            return InteractionResult.Ok();
        }

        public InteractionResult TryDrop(ActionContext ctx, string itemEntityId)
        {
            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemEntityId, out var entity))
                return InteractionResult.Fail("Item not in inventory");

            entity.Set(new WorldLocation(ctx.ViewLocation.X, ctx.ViewLocation.Y, ctx.ViewLocation.Z));
            ctx.World.AddEntity(entity);

            inventory.Remove(itemEntityId);
            return InteractionResult.Ok();
        }

        public InteractionResult TryOpen(ActionContext ctx, string targetEntityId)
            => ToggleDoor(ctx, targetEntityId, open: true);

        public InteractionResult TryClose(ActionContext ctx, string targetEntityId)
            => ToggleDoor(ctx, targetEntityId, open: false);

        /// <summary>
        /// Fails unless <paramref name="target"/> is at the actor's location or an
        /// adjacent cardinal cell on the same Z level. This is the range rule for
        /// direct physical interactions (doors, locks, activation) — without it a
        /// client could act on any entity map-wide by ID. Returns null when in range.
        /// </summary>
        private static InteractionResult? NotWithinReach(ActionContext ctx, Entity target)
        {
            var targetLoc = target.Get<WorldLocation>();
            if (targetLoc == null)
                return InteractionResult.Fail("Target has no location");

            var distance = System.Math.Abs(targetLoc.X - ctx.ViewLocation.X) +
                           System.Math.Abs(targetLoc.Y - ctx.ViewLocation.Y);
            if (distance > 1 || targetLoc.Z != ctx.ViewLocation.Z)
                return InteractionResult.Fail("Too far away");

            return null;
        }

        public InteractionResult TryUse(GameSession session, string itemEntityId, string onEntityId, string? usageId = null)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryUse(new ActionContext(session.World, session.Player, session.ViewLocation), itemEntityId, onEntityId, usageId);
        }

        public InteractionResult TryUse(ActionContext ctx, string itemEntityId, string onEntityId, string? usageId = null)
        {
            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemEntityId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!ctx.World.Entities.TryGetValue(onEntityId, out var target))
                return InteractionResult.Fail("Target not found");

            // Get all valid usage options
            var options = GetUseOptions(ctx, itemEntityId, onEntityId);

            // If no options, fail
            if (options.Count == 0)
                return InteractionResult.Fail("No effect");

            // If usageId is provided, use it directly
            if (!string.IsNullOrEmpty(usageId))
            {
                return TryUseWithMode(ctx, itemEntityId, onEntityId, usageId);
            }

            // If exactly one option, auto-execute (backward compatibility)
            if (options.Count == 1)
            {
                return TryUseWithMode(ctx, itemEntityId, onEntityId, options[0].UsageId);
            }

            // Multiple options: return them for reactive disambiguation
            return InteractionResult.OptionsResult(options);
        }

        /// <summary>
        /// Gets all valid usage options for an item and optional target, considering context.
        /// </summary>
        public List<UseOption> GetUseOptions(GameSession session, string itemEntityId, string? targetEntityId = null)
        {
            if (session.Player == null || session.ViewLocation == null)
                return new List<UseOption>();
            return GetUseOptions(new ActionContext(session.World, session.Player, session.ViewLocation), itemEntityId, targetEntityId);
        }

        public List<UseOption> GetUseOptions(ActionContext ctx, string itemEntityId, string? targetEntityId = null)
        {
            var options = new List<UseOption>();

            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null || !inventory.Items.TryGetValue(itemEntityId, out var item))
                return options;

            var contextTags = ContextEvaluator.EvaluateContext(ctx.World, ctx.ViewLocation, targetEntityId);
            Entity? target = null;
            var hasTarget = !string.IsNullOrEmpty(targetEntityId) && ctx.World.Entities.TryGetValue(targetEntityId, out target);

            // Consume (no target required)
            var consumable = item.AllComponents.OfType<Consumable>().FirstOrDefault();
            if (consumable != null && consumable.Uses > 0)
            {
                options.Add(new UseOption
                {
                    UsageId = "consume",
                    Label = "Consume",
                    Description = "Consume this item",
                    ContextRequirements = new List<string>()
                });
            }

            // Place (no target required, but requires PlaceableLight)
            var placeableLight = item.AllComponents.OfType<PlaceableLight>().FirstOrDefault();
            if (placeableLight != null)
            {
                options.Add(new UseOption
                {
                    UsageId = "place",
                    Label = "Place",
                    Description = "Place this item at current location",
                    ContextRequirements = new List<string>()
                });
            }

            if (hasTarget && target != null)
            {
                // Unlock door (Key + locked door)
                var key = item.AllComponents.OfType<Key>().FirstOrDefault();
                var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                if (key != null && door != null && door.IsLocked && 
                    !string.IsNullOrEmpty(door.KeyShape) && door.KeyShape == key.KeyId)
                {
                    options.Add(new UseOption
                    {
                        UsageId = "unlock-door",
                        Label = "Unlock Door",
                        Description = "Unlock the door with this key",
                        ContextRequirements = new List<string> { "target-is-door", "adjacent-target" }
                    });
                }

                // Lockpick (Lockpick + locked door)
                var lockpick = item.AllComponents.OfType<Lockpick>().FirstOrDefault();
                if (lockpick != null && door != null && door.IsLocked)
                {
                    options.Add(new UseOption
                    {
                        UsageId = "lockpick",
                        Label = "Lockpick",
                        Description = "Attempt to pick the lock",
                        ContextRequirements = new List<string> { "target-is-door", "adjacent-target" }
                    });
                }

                // Force open (ForcesDoor component + door)
                var forcesDoor = item.AllComponents.OfType<ForcesDoor>().FirstOrDefault();
                if (forcesDoor != null && door != null && forcesDoor.Strength > 0)
                {
                    options.Add(new UseOption
                    {
                        UsageId = "force-open",
                        Label = "Force Open",
                        Description = "Force open the door",
                        ContextRequirements = new List<string> { "target-is-door", "adjacent-target" }
                    });
                }
            }

            // Filter options based on context requirements
            var validOptions = options.Where(opt =>
                ContextEvaluator.MeetsRequirements(contextTags, opt.ContextRequirements)
            ).ToList();

            return validOptions;
        }

        /// <summary>
        /// Executes a use action with a specific usage mode.
        /// </summary>
        public InteractionResult TryUseWithMode(GameSession session, string itemEntityId, string? targetEntityId, string usageId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryUseWithMode(new ActionContext(session.World, session.Player, session.ViewLocation), itemEntityId, targetEntityId, usageId);
        }

        public InteractionResult TryUseWithMode(ActionContext ctx, string itemEntityId, string? targetEntityId, string usageId)
        {
            switch (usageId.ToLowerInvariant())
            {
                case "consume":
                    return TryConsume(ctx, itemEntityId);

                case "place":
                    return TryPlace(ctx, itemEntityId);

                case "unlock-door":
                    // Extract key unlock logic to avoid recursion
                    if (string.IsNullOrEmpty(targetEntityId))
                        return InteractionResult.Fail("Target required for unlock-door");

                    var inventory = ctx.Player.Get<Inventory>();
                    if (inventory == null || !inventory.Items.TryGetValue(itemEntityId, out var item))
                        return InteractionResult.Fail("Item not in inventory");

                    if (!ctx.World.Entities.TryGetValue(targetEntityId, out var target))
                        return InteractionResult.Fail("Target not found");

                    // Direct TryUseWithMode calls bypass GetUseOptions' context
                    // filtering, so enforce reach here — a key must not unlock a
                    // door on the other side of the map.
                    var keyOutOfRange = NotWithinReach(ctx, target);
                    if (keyOutOfRange != null)
                        return keyOutOfRange;

                    var key = item.AllComponents.OfType<Key>().FirstOrDefault();
                    var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                    if (key != null && door != null)
                    {
                        if (!string.IsNullOrEmpty(door.KeyShape) && door.KeyShape == key.KeyId)
                        {
                            door.IsLocked = false;

                            // Emit world event for door unlocked
                            ctx.World.EmitEvent(new WorldEvent
                            {
                                EventType = WorldEventType.DoorUnlocked,
                                Location = target.Get<WorldLocation>(),
                                Entity = target
                            });

                            return InteractionResult.Ok();
                        }
                        return InteractionResult.Fail("Key does not match");
                    }

                    return InteractionResult.Fail("Key or door not found");

                case "lockpick":
                    if (string.IsNullOrEmpty(targetEntityId))
                        return InteractionResult.Fail("Target required for lockpick");
                    return TryLockpick(ctx, itemEntityId, targetEntityId);

                case "force-open":
                    if (string.IsNullOrEmpty(targetEntityId))
                        return InteractionResult.Fail("Target required for force-open");
                    return TryForceOpen(ctx, itemEntityId, targetEntityId);

                default:
                    return InteractionResult.Fail($"Unknown usage mode: {usageId}");
            }
        }

        private InteractionResult ToggleDoor(ActionContext ctx, string targetEntityId, bool open)
        {
            if (!ctx.World.Entities.TryGetValue(targetEntityId, out var target))
                return InteractionResult.Fail("Entity not found");

            // Direct open/close is a physical act — the actor must be next to the
            // door. (ToggleDoorCore stays unchecked: TryActivate's lever mechanisms
            // legitimately open doors at a distance via TargetEntityIds.)
            var outOfRange = NotWithinReach(ctx, target);
            if (outOfRange != null)
                return outOfRange;

            return ToggleDoorCore(ctx, targetEntityId, open);
        }

        private InteractionResult ToggleDoorCore(ActionContext ctx, string targetEntityId, bool open)
        {
            if (!ctx.World.Entities.TryGetValue(targetEntityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var door = target.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (door == null)
                return InteractionResult.Fail("Not a door");

            if (door.IsLocked)
                return InteractionResult.Fail("Locked");

            door.IsOpen = open;
            var world = ctx.World;
            if (open)
            {
                target.Clear<ObstructsView>();
                target.Clear<ObstructsMovement>();
                var tile = target.Get<Tile>();
                if (tile != null && world.TileTypes.ContainsKey("Indoors"))
                    tile.Type = world.TileTypes["Indoors"];

                world.EmitEvent(new WorldEvent
                {
                    EventType = WorldEventType.DoorOpened,
                    Location = target.Get<WorldLocation>(),
                    Entity = target
                });
            }
            else
            {
                target.Set(new ObstructsView { Opacity = 1 });
                target.Set(new ObstructsMovement { Obstruction = 1 });
                var tile = target.Get<Tile>();
                if (tile != null && world.TileTypes.ContainsKey("Wall"))
                    tile.Type = world.TileTypes["Wall"];

                world.EmitEvent(new WorldEvent
                {
                    EventType = WorldEventType.DoorClosed,
                    Location = target.Get<WorldLocation>(),
                    Entity = target
                });
            }
            return InteractionResult.Ok();
        }

        public InteractionResult TryActivate(GameSession session, string entityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryActivate(new ActionContext(session.World, session.Player, session.ViewLocation), entityId);
        }

        public InteractionResult TryActivate(ActionContext ctx, string entityId)
        {
            if (!ctx.World.Entities.TryGetValue(entityId, out var target))
                return InteractionResult.Fail("Entity not found");

            // The activatable itself must be within reach (including same Z —
            // previously only X/Y were checked, so a lever one floor away worked).
            var outOfRange = NotWithinReach(ctx, target);
            if (outOfRange != null)
                return outOfRange;

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
                if (ctx.World.Entities.TryGetValue(targetId, out var targetEntity))
                {
                    // If target is a door, unlock/open it
                    var door = targetEntity.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                    if (door != null && activatable.IsActivated)
                    {
                        door.IsLocked = false;
                        if (!door.IsOpen)
                        {
                            // Mechanism-driven: the lever was reach-checked above;
                            // its linked door may legitimately be far away.
                            ToggleDoorCore(ctx, targetId, open: true);
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
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryConsume(new ActionContext(session.World, session.Player, session.ViewLocation), itemId);
        }

        public InteractionResult TryConsume(ActionContext ctx, string itemId)
        {
            var inventory = ctx.Player.Get<Inventory>();
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
            var health = ctx.Player.Get<Health>();
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
            return TryPlace(new ActionContext(session.World, session.Player, session.ViewLocation), itemId, location);
        }

        public InteractionResult TryPlace(ActionContext ctx, string itemId, WorldLocation? location = null)
        {
            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            var placeLocation = location ?? ctx.ViewLocation;

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
            ctx.World.AddEntity(item);

            return InteractionResult.Ok();
        }

        public InteractionResult TryClimb(GameSession session, string entityId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryClimb(new ActionContext(session.World, session.Player, session.ViewLocation), entityId);
        }

        public InteractionResult TryClimb(ActionContext ctx, string entityId)
        {
            if (!ctx.World.Entities.TryGetValue(entityId, out var target))
                return InteractionResult.Fail("Entity not found");

            var climbable = target.AllComponents.OfType<Climbable>().FirstOrDefault();
            if (climbable == null)
                return InteractionResult.Fail("Not climbable");

            if (climbable.RequiresItem && !string.IsNullOrEmpty(climbable.RequiredItemId))
            {
                var inventory = ctx.Player.Get<Inventory>();
                if (inventory == null || !inventory.Items.ContainsKey(climbable.RequiredItemId))
                    return InteractionResult.Fail("Required item not in inventory");
            }

            // Climbing logic - for now, just verify it's at the same location
            var targetLoc = target.Get<WorldLocation>();
            if (targetLoc != ctx.ViewLocation)
                return InteractionResult.Fail("Not at climbable location");

            // Climbing would typically change Z level, but that's handled by movement system
            // This interaction just verifies the climb can be attempted
            return InteractionResult.Ok();
        }

        public InteractionResult TryForceOpen(GameSession session, string itemId, string doorId)
        {
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryForceOpen(new ActionContext(session.World, session.Player, session.ViewLocation), itemId, doorId);
        }

        public InteractionResult TryForceOpen(ActionContext ctx, string itemId, string doorId)
        {
            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!ctx.World.Entities.TryGetValue(doorId, out var door))
                return InteractionResult.Fail("Door not found");

            var outOfRange = NotWithinReach(ctx, door);
            if (outOfRange != null)
                return outOfRange;

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
                    ToggleDoor(ctx, doorId, open: true);
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
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryLockpick(new ActionContext(session.World, session.Player, session.ViewLocation), itemId, doorId);
        }

        public InteractionResult TryLockpick(ActionContext ctx, string itemId, string doorId)
        {
            var inventory = ctx.Player.Get<Inventory>();
            if (inventory == null)
                return InteractionResult.Fail("No inventory");

            if (!inventory.Items.TryGetValue(itemId, out var item))
                return InteractionResult.Fail("Item not in inventory");

            if (!ctx.World.Entities.TryGetValue(doorId, out var door))
                return InteractionResult.Fail("Door not found");

            var outOfRange = NotWithinReach(ctx, door);
            if (outOfRange != null)
                return outOfRange;

            var lockpick = item.AllComponents.OfType<Lockpick>().FirstOrDefault();
            if (lockpick == null)
                return InteractionResult.Fail("Item is not a lockpick");

            var doorComp = door.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
            if (doorComp == null || !doorComp.IsLocked)
                return InteractionResult.Fail("Door is not locked");

            // Simplified lockpicking: success based on skill level (60% + 5% per skill level).
            // Uses the injected IRandomSource so tests can drive deterministic outcomes
            // (see openspec/changes/extend-delta-vocabulary-for-use-disambiguation).
            var successChance = 0.6 + (lockpick.SkillLevel * 0.05);
            var roll = _random.NextDouble();

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
            if (session.Player == null || session.ViewLocation == null)
                return InteractionResult.Fail("No player or view location");
            return TryEquip(new ActionContext(session.World, session.Player, session.ViewLocation), itemId);
        }

        public InteractionResult TryEquip(ActionContext ctx, string itemId)
        {
            var inventory = ctx.Player.Get<Inventory>();
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
                ctx.Player.Set(item);
                return InteractionResult.Ok();
            }

            return InteractionResult.Fail("Item cannot be equipped");
        }
    }
}



