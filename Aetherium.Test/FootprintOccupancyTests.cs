using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;

namespace Aetherium.Test
{
    /// <summary>
    /// Multi-tile footprint occupancy (add-boardable-vehicles Phase 1): an entity carrying a
    /// <see cref="Footprint"/> is indexed at every tile it covers, placement/move validate every tile,
    /// two footprints never overlap, and single-tile entities keep their unchanged fast path.
    /// Pure engine-core tests over <see cref="World"/> — no Orleans.
    /// </summary>
    [TestFixture]
    public class FootprintOccupancyTests
    {
        // A concrete, non-Character entity to stand in for a vehicle exterior.
        private sealed class Hull : Entity { }

        private static World OpenWorld() => new FovDiagnosticWorldBuilder("open_space").Build();

        private static Hull FootprintAt(int x, int y, int w, int l)
        {
            var hull = new Hull();
            hull.Set(new WorldLocation(x, y, 0));
            // Size3d is (length, width, depth): +Y extent, +X extent, +Z extent.
            hull.Set(new Footprint { Size = new Size3d(l, w, 1) });
            return hull;
        }

        private static bool IndexedAt(World world, string id, int x, int y) =>
            world.EntitiesByLocation.TryGetValue(new WorldLocation(x, y, 0), out var bucket)
            && bucket.ContainsKey(id);

        // ----- placement -----------------------------------------------

        [Test]
        public void Place_IndexesEveryFootprintTile()
        {
            var world = OpenWorld(); // 30x30 Indoors, open around (16,16)
            var hull = FootprintAt(16, 16, w: 2, l: 2);

            Assert.That(world.TryPlace(hull), Is.True, "a footprint on clear passable ground must place");

            foreach (var (x, y) in new[] { (16, 16), (17, 16), (16, 17), (17, 17) })
                Assert.That(IndexedAt(world, hull.EntityId, x, y), Is.True, $"must be indexed at ({x},{y})");
            Assert.That(world.Entities.ContainsKey(hull.EntityId), Is.True);
        }

        [Test]
        public void Place_BlockedWhenAnyTileImpassable_IndexesNothing()
        {
            var world = OpenWorld();
            world.SetTerrain("Wall", new WorldLocation(17, 16, 0)); // one tile under the footprint is solid
            var hull = FootprintAt(16, 16, w: 2, l: 2);

            Assert.That(world.TryPlace(hull), Is.False, "an impassable tile under the footprint must block placement");

            Assert.That(world.Entities.ContainsKey(hull.EntityId), Is.False, "a blocked placement must not add the entity");
            foreach (var (x, y) in new[] { (16, 16), (17, 16), (16, 17), (17, 17) })
                Assert.That(IndexedAt(world, hull.EntityId, x, y), Is.False, $"must NOT be indexed at ({x},{y})");
        }

        [Test]
        public void Place_BlockedWhenAnyTileOffMap()
        {
            var world = OpenWorld(); // valid cells are x,y in [0,29]
            var hull = FootprintAt(29, 29, w: 2, l: 2); // (30,29),(29,30),(30,30) are void

            Assert.That(world.TryPlace(hull), Is.False, "a footprint spilling off the map must not place");
            Assert.That(world.Entities.ContainsKey(hull.EntityId), Is.False);
        }

        [Test]
        public void Place_BlockedWhenOverlappingAnotherFootprint()
        {
            var world = OpenWorld();
            var a = FootprintAt(16, 16, w: 2, l: 2); // (16..17, 16..17)
            Assert.That(world.TryPlace(a), Is.True);

            var b = FootprintAt(17, 16, w: 2, l: 2); // (17..18, 16..17) — overlaps a at (17,16),(17,17)
            Assert.That(world.TryPlace(b), Is.False, "two footprints must not overlap");
            Assert.That(world.Entities.ContainsKey(b.EntityId), Is.False);
        }

        // ----- movement ------------------------------------------------

        [Test]
        public void Move_ReindexesTiles_ReleasingThePrevious()
        {
            var world = OpenWorld();
            var hull = FootprintAt(16, 16, w: 2, l: 2); // (16..17, 16..17)
            Assert.That(world.TryPlace(hull), Is.True);

            Assert.That(world.TryMoveFootprint(hull, new WorldLocation(16, 17, 0)), Is.True); // -> (16..17, 17..18)

            // Old-only tiles released.
            Assert.That(IndexedAt(world, hull.EntityId, 16, 16), Is.False, "released (16,16)");
            Assert.That(IndexedAt(world, hull.EntityId, 17, 16), Is.False, "released (17,16)");
            // Newly occupied tiles indexed.
            Assert.That(IndexedAt(world, hull.EntityId, 16, 18), Is.True, "occupies (16,18)");
            Assert.That(IndexedAt(world, hull.EntityId, 17, 18), Is.True, "occupies (17,18)");
            // Overlap tiles stay indexed.
            Assert.That(IndexedAt(world, hull.EntityId, 16, 17), Is.True, "keeps (16,17)");
            Assert.That(IndexedAt(world, hull.EntityId, 17, 17), Is.True, "keeps (17,17)");
            // Anchor moved.
            Assert.That(hull.Get<WorldLocation>(), Is.EqualTo(new WorldLocation(16, 17, 0)));
        }

        [Test]
        public void Move_BlockedWhenDestinationTileOccupiedByCharacter_LeavesEntityPut()
        {
            var world = OpenWorld();
            var hull = FootprintAt(16, 16, w: 2, l: 2);
            Assert.That(world.TryPlace(hull), Is.True);

            var bystander = new Character();
            bystander.Set(new WorldLocation(16, 18, 0)); // a destination tile if the hull moves +1 in Y
            world.AddEntity(bystander);

            Assert.That(world.TryMoveFootprint(hull, new WorldLocation(16, 17, 0)), Is.False,
                "a character standing on a destination tile must block the footprint move");

            // Unchanged: still at the original anchor and tiles.
            Assert.That(hull.Get<WorldLocation>(), Is.EqualTo(new WorldLocation(16, 16, 0)));
            Assert.That(IndexedAt(world, hull.EntityId, 16, 16), Is.True);
            Assert.That(IndexedAt(world, hull.EntityId, 17, 17), Is.True);
            Assert.That(IndexedAt(world, hull.EntityId, 16, 18), Is.False, "must not have crept onto the blocked tile");
        }

        [Test]
        public void Remove_UnindexesEveryFootprintTile()
        {
            var world = OpenWorld();
            var hull = FootprintAt(16, 16, w: 2, l: 2);
            Assert.That(world.TryPlace(hull), Is.True);

            Assert.That(world.TryRemoveEntity(hull.EntityId), Is.True);

            Assert.That(world.Entities.ContainsKey(hull.EntityId), Is.False);
            foreach (var (x, y) in new[] { (16, 16), (17, 16), (16, 17), (17, 17) })
                Assert.That(IndexedAt(world, hull.EntityId, x, y), Is.False, $"must be released from ({x},{y})");
        }

        // ----- single-tile fast path -----------------------------------

        [Test]
        public void SingleTileEntity_KeepsSingleAnchorIndexing()
        {
            var world = OpenWorld();
            var lone = new Character();
            lone.Set(new WorldLocation(16, 16, 0));
            world.AddEntity(lone); // no Footprint -> fast path

            Assert.That(IndexedAt(world, lone.EntityId, 16, 16), Is.True);
            Assert.That(IndexedAt(world, lone.EntityId, 17, 16), Is.False, "a footprint-less entity occupies only its anchor");

            // A footprint placed right next to it is unaffected by the lone entity's single tile.
            var hull = FootprintAt(17, 16, w: 2, l: 2); // (17..18, 16..17) — does not include (16,16)
            Assert.That(world.TryPlace(hull), Is.True);
        }
    }
}
