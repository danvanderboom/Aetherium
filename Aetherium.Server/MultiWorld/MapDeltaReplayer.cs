using System;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Applies a <see cref="MapDelta"/> to a grain-side <see cref="World"/> during cold-start
    /// recovery. Used by <see cref="GameMapGrain"/> to replay the post-snapshot delta log atop
    /// a snapshot-hydrated world.
    ///
    /// <para>
    /// Differs from <see cref="Aetherium.Server.GameSession.ApplyDelta"/> in that the grain
    /// has no concept of a "local Player" — owner-side inventory transitions look up the
    /// owning character in <c>World.Entities</c> by id rather than matching against a local
    /// session.
    /// </para>
    ///
    /// <para>
    /// Every delta is idempotent (per the world-persistence spec): replaying a delta whose
    /// effect is already present is a no-op. Unknown <c>ComponentFieldChangedDelta</c>
    /// pairs throw <see cref="NotImplementedException"/> so test failures are loud.
    /// </para>
    /// </summary>
    public static class MapDeltaReplayer
    {
        public static void Apply(World world, MapDelta delta)
        {
            switch (delta)
            {
                case EntityAddedDelta added: ApplyEntityAdded(world, added); break;
                case EntityRemovedDelta removed: world.TryRemoveEntity(removed.EntityId); break;
                case EntityMovedDelta moved: ApplyEntityMoved(world, moved); break;
                case EntityHeadingChangedDelta heading: ApplyHeadingChanged(world, heading); break;
                case DoorStateChangedDelta door: ApplyDoorStateChanged(world, door); break;
                case ItemTransferredDelta xfer: ApplyItemTransferred(world, xfer); break;
                case ComponentFieldChangedDelta field: ApplyComponentFieldChanged(world, field); break;
                case ItemDestroyedDelta destroyed: ApplyItemDestroyed(world, destroyed); break;
                case EntityPlacedDelta placed: ApplyEntityPlaced(world, placed); break;

                // Heat trails are grain-authoritative and rebuilt from sensor data on activation;
                // skipping HeatRecordedDelta / HeatExpiredDelta during cold-start replay is safe.
                default:
                    break;
            }
        }

        private static void ApplyEntityAdded(World world, EntityAddedDelta delta)
        {
            if (delta.Placement is null) return;
            if (world.Entities.ContainsKey(delta.Placement.EntityId)) return; // idempotent
            var factory = new EntityFactory(world);
            var entity = factory.Create(delta.Placement);
            if (entity is not null) world.AddEntity(entity);
        }

        private static void ApplyEntityMoved(World world, EntityMovedDelta delta)
        {
            if (!world.Entities.ContainsKey(delta.EntityId)) return;
            world.MoveEntity(delta.EntityId, new WorldLocation(delta.NewX, delta.NewY, delta.NewZ));
        }

        private static void ApplyHeadingChanged(World world, EntityHeadingChangedDelta delta)
        {
            if (!world.Entities.TryGetValue(delta.EntityId, out var entity)) return;
            var heading = entity.Get<HasHeading>();
            if (heading is not null) heading.Heading = delta.Degrees;
        }

        private static void ApplyDoorStateChanged(World world, DoorStateChangedDelta delta)
        {
            if (!world.Entities.TryGetValue(delta.EntityId, out var entity)) return;
            var oc = entity.Get<OpensAndCloses>();
            if (oc is not null)
            {
                oc.IsOpen = delta.IsOpen;
                oc.IsLocked = delta.IsLocked;
            }
        }

        private static void ApplyItemTransferred(World world, ItemTransferredDelta delta)
        {
            if (delta.IntoInventory)
            {
                world.TryRemoveEntity(delta.ItemEntityId);
                if (!string.IsNullOrEmpty(delta.OwnerEntityId) &&
                    world.Entities.TryGetValue(delta.OwnerEntityId, out var owner))
                {
                    var inv = owner.Get<Inventory>();
                    if (inv is not null && delta.ItemPlacement is not null)
                    {
                        var factory = new EntityFactory(world);
                        var item = factory.Create(delta.ItemPlacement);
                        if (item is not null) inv.TryAdd(item.EntityId, item);
                    }
                }
            }
            else
            {
                // inventory → world. Remove from owner's inventory then add to world.
                if (!string.IsNullOrEmpty(delta.OwnerEntityId) &&
                    world.Entities.TryGetValue(delta.OwnerEntityId, out var owner))
                {
                    var inv = owner.Get<Inventory>();
                    inv?.Remove(delta.ItemEntityId);
                }
                if (delta.ItemPlacement is not null && !world.Entities.ContainsKey(delta.ItemEntityId))
                {
                    var factory = new EntityFactory(world);
                    var item = factory.Create(delta.ItemPlacement);
                    if (item is not null) world.AddEntity(item);
                }
            }
        }

        private static void ApplyItemDestroyed(World world, ItemDestroyedDelta delta)
        {
            if (!string.IsNullOrEmpty(delta.OwnerEntityId) &&
                world.Entities.TryGetValue(delta.OwnerEntityId, out var owner))
            {
                var inv = owner.Get<Inventory>();
                inv?.Remove(delta.EntityId);
                return;
            }
            world.TryRemoveEntity(delta.EntityId);
        }

        private static void ApplyEntityPlaced(World world, EntityPlacedDelta delta)
        {
            if (delta.Placement is null) return;

            if (!string.IsNullOrEmpty(delta.SourceOwnerEntityId) &&
                world.Entities.TryGetValue(delta.SourceOwnerEntityId, out var owner))
            {
                var inv = owner.Get<Inventory>();
                inv?.Remove(delta.Placement.EntityId);
            }

            if (world.Entities.ContainsKey(delta.Placement.EntityId)) return;
            var factory = new EntityFactory(world);
            var entity = factory.Create(delta.Placement);
            if (entity is not null) world.AddEntity(entity);
        }

        private static void ApplyComponentFieldChanged(World world, ComponentFieldChangedDelta delta)
        {
            var entity = FindEntityAnywhere(world, delta.EntityId);
            if (entity is null) return;

            var key = (delta.ComponentType, delta.FieldName);
            switch (key)
            {
                case ("Consumable", "Uses"):
                    if (entity.Get<Consumable>() is { } cons && delta.NumericValue.HasValue) cons.Uses = (int)delta.NumericValue.Value; break;
                case ("Health", "Level"):
                    if (entity.Get<Health>() is { } h && delta.NumericValue.HasValue) h.Level = (int)delta.NumericValue.Value; break;
                case ("ForcesDoor", "Durability"):
                    if (entity.Get<ForcesDoor>() is { } fd && delta.NumericValue.HasValue) fd.Durability = (int)delta.NumericValue.Value; break;
                case ("Lockpick", "Durability"):
                    if (entity.Get<Lockpick>() is { } lp && delta.NumericValue.HasValue) lp.Durability = (int)delta.NumericValue.Value; break;
                case ("PlaceableLight", "IsPlaced"):
                    if (entity.Get<PlaceableLight>() is { } pl && delta.BoolValue.HasValue) pl.IsPlaced = delta.BoolValue.Value; break;
                case ("LightSource", "IsEnabled"):
                    if (entity.Get<LightSource>() is { } ls1 && delta.BoolValue.HasValue) ls1.IsEnabled = delta.BoolValue.Value; break;
                case ("LightSource", "IsDynamic"):
                    if (entity.Get<LightSource>() is { } ls2 && delta.BoolValue.HasValue) ls2.IsDynamic = delta.BoolValue.Value; break;
                case ("Activatable", "IsActivated"):
                    if (entity.Get<Activatable>() is { } act && delta.BoolValue.HasValue) act.IsActivated = delta.BoolValue.Value; break;
                case ("Inventory", "Capacity"):
                    if (entity.Get<Inventory>() is { } inv && delta.NumericValue.HasValue) inv.Capacity = (int)delta.NumericValue.Value; break;
                default:
                    throw new NotImplementedException(
                        $"ComponentFieldChangedDelta for ({delta.ComponentType}.{delta.FieldName}) is not handled by MapDeltaReplayer. " +
                        "Add a case here (and in GameSession.ApplyComponentFieldChanged) if this field is meant to be replayable.");
            }
        }

        private static Entity? FindEntityAnywhere(World world, string entityId)
        {
            if (world.Entities.TryGetValue(entityId, out var worldEntity)) return worldEntity;
            foreach (var e in world.Entities.Values)
            {
                var inv = e.Get<Inventory>();
                if (inv is not null && inv.Items.TryGetValue(entityId, out var item)) return item;
            }
            return null;
        }
    }
}
