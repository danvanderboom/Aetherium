using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.MultiWorld;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 2 of add-flying-entities: airborne movement. Verifies that flyers traverse over impassable
    /// ground through open air, change altitude freely within their band range (no CanAscend/CanDescend),
    /// are clamped to that range, and that grounded movement is unchanged.
    /// </summary>
    public class FlightMovementTests
    {
        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static Character MakeFlyer(World world, WorldLocation at, int minBand = 1, int maxBand = 5)
        {
            var flyer = new Character();
            flyer.Set(at);
            flyer.Set(new Flight { MinBand = minBand, MaxBand = maxBand, CruiseBand = at.Z, State = FlightState.Airborne });
            world.AddEntity(flyer);
            return flyer;
        }

        [Test]
        public void AirborneFlyer_TraversesOverImpassableGround_ThroughOpenAir()
        {
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(1, 0, 0)); // impassable ground, height = DefaultWallHeight (3)

            int above = world.DefaultWallHeight; // band clear of the wall top
            var flyer = MakeFlyer(world, new WorldLocation(0, 0, above));

            var moved = world.TryMove(flyer, new WorldLocation(1, 0, above)); // over the mountain, in open air

            Assert.IsTrue(moved, "A flyer above the wall traverses over impassable ground.");
            Assert.AreEqual(new WorldLocation(1, 0, above), flyer.Get<WorldLocation>());
            Assert.IsTrue(world.EntitiesByLocation.ContainsKey(new WorldLocation(1, 0, above)),
                "Moving into open air creates the destination cell.");
        }

        [Test]
        public void AirborneFlyer_BlockedWhenBandIntersectsObstruction()
        {
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(1, 0, 0)); // height 3 => blocks bands 0,1,2

            int inside = world.DefaultWallHeight - 1; // still within the wall
            var flyer = MakeFlyer(world, new WorldLocation(0, 0, inside));

            var moved = world.TryMove(flyer, new WorldLocation(1, 0, inside));

            Assert.IsFalse(moved, "A flyer whose band still intersects the wall is blocked.");
            Assert.AreEqual(new WorldLocation(0, 0, inside), flyer.Get<WorldLocation>());
        }

        [Test]
        public void AirborneFlyer_ChangesAltitudeFreely_WithinBandRange_NoAscendMarker()
        {
            var world = MakeWorld();
            var flyer = MakeFlyer(world, new WorldLocation(0, 0, 2), minBand: 1, maxBand: 5);

            // No CanAscend/CanDescend markers exist, yet vertical moves within range succeed.
            Assert.IsTrue(world.TryMove(flyer, new WorldLocation(0, 0, 3)), "Airborne climb needs no CanAscend marker.");
            Assert.AreEqual(3, flyer.Get<WorldLocation>().Z);

            Assert.IsTrue(world.TryMove(flyer, new WorldLocation(0, 0, 2)), "Airborne descend needs no CanDescend marker.");
            Assert.AreEqual(2, flyer.Get<WorldLocation>().Z);
        }

        [Test]
        public void AirborneFlyer_ClampedToBandRange()
        {
            var world = MakeWorld();

            var high = MakeFlyer(world, new WorldLocation(0, 0, 5), minBand: 1, maxBand: 5);
            Assert.IsFalse(world.TryMove(high, new WorldLocation(0, 0, 6)), "Cannot climb above MaxBand.");
            Assert.AreEqual(5, high.Get<WorldLocation>().Z);

            var low = MakeFlyer(world, new WorldLocation(2, 2, 1), minBand: 1, maxBand: 5);
            Assert.IsFalse(world.TryMove(low, new WorldLocation(2, 2, 0)), "Cannot descend below MinBand while airborne.");
            Assert.AreEqual(1, low.Get<WorldLocation>().Z);
        }

        [Test]
        public void GroundedCharacter_CannotEnterOpenAir()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(0, 0, 0));

            var walker = new Character(); // no Flight
            walker.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(walker);

            // (0,1,0) has no terrain entity: a grounded character may not move there.
            var moved = world.TryMove(walker, new WorldLocation(0, 1, 0));

            Assert.IsFalse(moved, "Grounded movement still requires a terrain-bearing destination cell.");
            Assert.AreEqual(new WorldLocation(0, 0, 0), walker.Get<WorldLocation>());
        }

        [Test]
        public void MoveEntity_IntoPreviouslyEmptyCell_CreatesBucketAndMoves()
        {
            var world = MakeWorld();
            var flyer = MakeFlyer(world, new WorldLocation(0, 0, 2));

            var target = new WorldLocation(3, 3, 4);
            Assert.IsFalse(world.EntitiesByLocation.ContainsKey(target), "Precondition: target cell is empty.");

            world.MoveEntity(flyer.EntityId, target);

            Assert.AreEqual(target, flyer.Get<WorldLocation>());
            Assert.IsTrue(world.EntitiesByLocation.TryGetValue(target, out var bucket) && bucket.ContainsKey(flyer.EntityId),
                "The entity is indexed at the newly-created destination cell.");
        }

        [Test]
        public void IsFlyingCreatureType_KnownFlyers_AreFlyers()
        {
            Assert.IsTrue(GameMapGrain.IsFlyingCreatureType("bird"));
            Assert.IsTrue(GameMapGrain.IsFlyingCreatureType("Satellite"));
            Assert.IsTrue(GameMapGrain.IsFlyingCreatureType("DRONE"));
            Assert.IsFalse(GameMapGrain.IsFlyingCreatureType("monster"));
            Assert.IsFalse(GameMapGrain.IsFlyingCreatureType(""));
        }
    }
}
