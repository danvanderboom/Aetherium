using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.MultiWorld;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Unit tests for the grain-side delta replayer used during cold-start recovery.
    /// Each delta type is tested in isolation against a small synthetic world.
    /// </summary>
    public class MapDeltaReplayerTests
    {
        private static (World world, Door door) WorldWithDoor()
        {
            var world = new World();
            var door = new Door();
            door.Set(new WorldLocation(2, 2, 0));
            world.AddEntity(door);
            return (world, door);
        }

        [Fact]
        public void DoorStateChangedDelta_Sets_IsOpen_And_IsLocked()
        {
            var (world, door) = WorldWithDoor();
            Assert.False(door.Get<OpensAndCloses>()!.IsOpen);

            MapDeltaReplayer.Apply(world, new DoorStateChangedDelta
            {
                EntityId = door.EntityId,
                IsOpen = true,
                IsLocked = false,
            });

            Assert.True(door.Get<OpensAndCloses>()!.IsOpen);
            Assert.False(door.Get<OpensAndCloses>()!.IsLocked);
        }

        [Fact]
        public void EntityRemovedDelta_Removes_From_World_And_Is_Idempotent_On_Missing()
        {
            var world = new World();
            var key = new KeyItem("k");
            key.Set(new WorldLocation(1, 1, 0));
            world.AddEntity(key);

            MapDeltaReplayer.Apply(world, new EntityRemovedDelta { EntityId = key.EntityId });
            Assert.False(world.Entities.ContainsKey(key.EntityId));

            // Replay on missing — silent, no throw.
            MapDeltaReplayer.Apply(world, new EntityRemovedDelta { EntityId = "ghost" });
        }

        [Fact]
        public void ComponentFieldChangedDelta_Updates_Consumable_Uses_In_Inventory()
        {
            var world = new World();
            var character = new Aetherium.Character();
            character.Set(new WorldLocation(0, 0, 0));
            character.Set(new Inventory { Capacity = 10 });
            world.AddEntity(character);

            var torch = new TorchItem();
            character.Get<Inventory>()!.TryAdd(torch.EntityId, torch);
            var startUses = torch.Get<Consumable>()!.Uses;

            MapDeltaReplayer.Apply(world, new ComponentFieldChangedDelta
            {
                EntityId = torch.EntityId,
                ComponentType = "Consumable",
                FieldName = "Uses",
                NumericValue = startUses - 1,
            });

            Assert.Equal(startUses - 1, torch.Get<Consumable>()!.Uses);
        }

        [Fact]
        public void EntityHeadingChangedDelta_Updates_HasHeading()
        {
            var world = new World();
            var character = new Aetherium.Character();
            character.Set(new WorldLocation(0, 0, 0));
            character.Set(new HasHeading());
            world.AddEntity(character);

            MapDeltaReplayer.Apply(world, new EntityHeadingChangedDelta
            {
                EntityId = character.EntityId,
                Degrees = 180,
            });

            Assert.Equal(180, character.Get<HasHeading>()!.Heading);
        }

        [Fact]
        public void ItemDestroyedDelta_Removes_From_Owner_Inventory_When_Owner_Present()
        {
            var world = new World();
            var owner = new Aetherium.Character();
            owner.Set(new WorldLocation(0, 0, 0));
            owner.Set(new Inventory { Capacity = 10 });
            world.AddEntity(owner);
            var item = new KeyItem("doomed");
            owner.Get<Inventory>()!.TryAdd(item.EntityId, item);

            MapDeltaReplayer.Apply(world, new ItemDestroyedDelta
            {
                EntityId = item.EntityId,
                OwnerEntityId = owner.EntityId,
            });

            Assert.False(owner.Get<Inventory>()!.Items.ContainsKey(item.EntityId));
        }

        [Fact]
        public void EntityAddedDelta_Materializes_Entity_From_Placement()
        {
            var world = new World();
            MapDeltaReplayer.Apply(world, new EntityAddedDelta
            {
                Placement = new EntityPlacement
                {
                    EntityId = "added-key",
                    TypeName = nameof(KeyItem),
                    X = 3, Y = 3, Z = 0,
                    Properties = new System.Collections.Generic.Dictionary<string, string> { ["KeyId"] = "red" },
                },
            });

            Assert.True(world.Entities.TryGetValue("added-key", out var entity));
            Assert.IsType<KeyItem>(entity);
        }
    }
}
