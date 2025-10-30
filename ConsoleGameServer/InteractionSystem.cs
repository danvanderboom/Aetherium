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
    }
}


