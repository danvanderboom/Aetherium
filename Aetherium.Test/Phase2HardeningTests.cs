using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Xunit;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for the Phase 2 hardening items (docs/audits/RECOMMENDATIONS.md):
    /// P0-9 inventory-capacity exploit, P0-10 MoveEntity index consistency, and
    /// P0-12 cross-path session serialization.
    /// </summary>
    public class Phase2HardeningTests
    {
        private static World CreateWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        // ---------- P0-9: inventory-capacity exploit ----------

        [Fact]
        public void TryEquip_CapacityBoost_AppliesOnce()
        {
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var inventory = session.Player!.Get<Inventory>()!;
            var baseCapacity = inventory.Capacity;

            var backpack = new Item { EntityId = "backpack" };
            backpack.Set(new CapacityBoost { AdditionalCapacity = 5 });
            inventory.TryAdd(backpack.EntityId, backpack);

            var system = new InteractionSystem();

            var first = system.TryEquip(session, backpack.EntityId);
            Assert.True(first.Success);
            Assert.Equal(baseCapacity + 5, inventory.Capacity);

            // Re-equipping the same backpack must NOT stack the bonus again.
            var second = system.TryEquip(session, backpack.EntityId);
            Assert.False(second.Success);
            Assert.Equal(baseCapacity + 5, inventory.Capacity);
        }

        [Fact]
        public void TryEquip_CapacityBoost_RepeatedCalls_DoNotGrantUnboundedCapacity()
        {
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            var inventory = session.Player!.Get<Inventory>()!;
            var baseCapacity = inventory.Capacity;

            var backpack = new Item { EntityId = "backpack" };
            backpack.Set(new CapacityBoost { AdditionalCapacity = 5 });
            inventory.TryAdd(backpack.EntityId, backpack);

            var system = new InteractionSystem();
            for (int i = 0; i < 100; i++)
                system.TryEquip(session, backpack.EntityId);

            Assert.Equal(baseCapacity + 5, inventory.Capacity);
        }

        // ---------- P0-10: MoveEntity index consistency ----------

        [Fact]
        public void MoveEntity_Reindexes_Entity_At_Destination()
        {
            var world = CreateWorld();
            var a = new WorldLocation(0, 0, 0);
            var b = new WorldLocation(1, 0, 0);
            world.SetTerrain("Indoors", a);
            world.SetTerrain("Indoors", b);

            var e = new Character();
            e.Set(a);
            world.AddEntity(e);

            world.MoveEntity(e.EntityId, b);

            Assert.True(world.EntitiesByLocation[b].ContainsKey(e.EntityId));
            Assert.False(world.EntitiesByLocation.TryGetValue(a, out var atA) && atA.ContainsKey(e.EntityId));
            Assert.Equal(b, e.Get<WorldLocation>());
        }

        [Fact]
        public void MoveEntity_Does_Not_Drop_Entity_When_Destination_Already_Indexes_It()
        {
            // Reproduces the P0-10 edge: a concurrent double-move already indexed the
            // entity at the destination. The old code's TryAdd would fail *after*
            // removing it from the source bucket, dropping it from the location index
            // entirely. The idempotent write must keep it indexed at the destination.
            var world = CreateWorld();
            var a = new WorldLocation(0, 0, 0);
            var b = new WorldLocation(1, 0, 0);
            world.SetTerrain("Indoors", a);
            world.SetTerrain("Indoors", b);

            var e = new Character();
            e.Set(a);
            world.AddEntity(e);

            // Simulate the racing writer having already placed e in b's bucket.
            world.EntitiesByLocation.GetOrAdd(b, _ => new ConcurrentDictionary<string, Entity>())[e.EntityId] = e;

            world.MoveEntity(e.EntityId, b);

            // e must still be indexed at b (not dropped), and gone from a.
            Assert.True(world.EntitiesByLocation[b].ContainsKey(e.EntityId));
            Assert.False(world.EntitiesByLocation.TryGetValue(a, out var atA) && atA.ContainsKey(e.EntityId));
        }

        // ---------- P0-12: cross-path serialization ----------

        [Fact]
        public void WithStateLock_Is_Reentrant_With_GetPerception()
        {
            // GetPerception takes the same lock; nesting it inside WithStateLock on
            // the same thread must not deadlock (Monitor is reentrant).
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));

            var perception = session.WithStateLock(() => session.GetPerception());

            Assert.NotNull(perception);
        }

        [Fact]
        public async Task Concurrent_Interactions_And_Perception_Do_Not_Corrupt_State()
        {
            // Legacy session hit from many threads at once (gateway pickups + moves +
            // perception snapshots). With WithStateLock serializing session mutation,
            // none of these should throw "collection was modified" or leave torn state.
            var session = new GameSession("test", new FovDiagnosticWorldBuilder("open_space"));
            TestWorldMovement.CarveOpenArea(session, radius: 4);
            var loc = session.Player!.Get<WorldLocation>()!;
            var gateway = new LocalMutationGateway(session);

            // Seed carriable items at the player's location for pickups.
            for (int i = 0; i < 20; i++)
            {
                var item = new Item { EntityId = $"item-{i}" };
                item.Set(new WorldLocation(loc.X, loc.Y, loc.Z));
                item.Set(new Carriable { Label = $"Item {i}", Icon = "*" });
                session.Player.Get<Inventory>()!.Capacity = 100;
                session.World.AddEntity(item);
            }

            var tasks = new System.Collections.Generic.List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var id = $"item-{i}";
                tasks.Add(Task.Run(() => gateway.PickupAsync(id)));
            }
            for (int i = 0; i < 20; i++)
                tasks.Add(Task.Run(() => { session.GetPerception(); }));

            // Must complete without throwing.
            await Task.WhenAll(tasks);

            // Every item ended up in exactly one place: inventory XOR world.
            var inv = session.Player.Get<Inventory>()!;
            for (int i = 0; i < 20; i++)
            {
                var id = $"item-{i}";
                var inInventory = inv.Items.ContainsKey(id);
                var inWorld = session.World.Entities.ContainsKey(id);
                Assert.True(inInventory ^ inWorld, $"{id} should be in exactly one of inventory/world");
            }
        }
    }
}
