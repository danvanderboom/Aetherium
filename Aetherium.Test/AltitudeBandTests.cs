using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Phase 1 of add-flying-entities: altitude bands &amp; layered obstruction.
    /// Verifies per-band obstruction resolution (movement/sight), the terrain height table, and that grounded
    /// (non-Flight) movement behaves exactly as before.
    /// </summary>
    public class AltitudeBandTests
    {
        // A minimal non-Terrain, non-Character entity used to place explicit obstructions (bridges, skylights).
        private sealed class TestObstacle : Entity { }

        private static World MakeWorld()
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test]
        public void TerrainObstructionHeight_Passable_IsZero_Impassable_IsWallHeight()
        {
            var world = MakeWorld();

            Assert.AreEqual(0, world.TerrainObstructionHeight(world.TerrainTypes["Indoors"]),
                "Passable terrain must not obstruct any band.");
            Assert.AreEqual(world.DefaultWallHeight, world.TerrainObstructionHeight(world.TerrainTypes["Mountain"]),
                "Impassable terrain without an explicit setting must use the default wall height.");
            Assert.AreEqual(world.DefaultWallHeight, world.TerrainObstructionHeight(null),
                "Unknown terrain is treated as solid.");
        }

        [Test]
        public void GroundObstacle_DoesNotObstruct_HigherBand()
        {
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(2, 2, 0)); // impassable, height = DefaultWallHeight

            int h = world.DefaultWallHeight;

            Assert.IsTrue(world.ColumnObstructsMovement(2, 2, 0), "Ground band is blocked by the wall.");
            Assert.IsTrue(world.ColumnObstructsMovement(2, 2, h - 1), "Bands within the wall height are blocked.");
            Assert.IsFalse(world.ColumnObstructsMovement(2, 2, h), "The band at/above the wall top is clear.");
        }

        [Test]
        public void PassableGround_DoesNotObstruct_AnyBand()
        {
            var world = MakeWorld();
            world.SetTerrain("Indoors", new WorldLocation(3, 3, 0)); // passable, height 0

            Assert.IsFalse(world.ColumnObstructsMovement(3, 3, 0), "Passable terrain does not obstruct band 0.");
            Assert.IsFalse(world.ColumnObstructsMovement(3, 3, 1), "Passable terrain does not obstruct band 1.");
        }

        [Test]
        public void Bridge_BlocksMovementAndSight_AtItsBand_HigherIsClear()
        {
            var world = MakeWorld();

            var bridge = new TestObstacle();
            bridge.Set(new WorldLocation(4, 4, 1));
            bridge.Set(new ObstructsMovement());                 // Obstruction 1, Height 1
            bridge.Set(new ObstructsView { Opacity = 1 });       // opaque, Height 1
            world.AddEntity(bridge);

            // Movement
            Assert.IsTrue(world.ColumnObstructsMovement(4, 4, 1), "The bridge blocks movement at its own band.");
            Assert.IsFalse(world.ColumnObstructsMovement(4, 4, 2), "The band above the bridge is clear.");

            // Sight
            Assert.AreEqual(1.0, world.ColumnViewOpacity(4, 4, 1), 1e-9, "The opaque bridge blocks sight at its band.");
            Assert.AreEqual(0.0, world.ColumnViewOpacity(4, 4, 2), 1e-9, "Sight is clear above the bridge.");
        }

        [Test]
        public void GlassSkylight_BlocksMovement_ButNotSight()
        {
            var world = MakeWorld();

            var skylight = new TestObstacle();
            skylight.Set(new WorldLocation(8, 8, 1));
            skylight.Set(new ObstructsMovement());               // solid: cannot walk through
            skylight.Set(new ObstructsView { Opacity = 0 });     // transparent: can see through
            world.AddEntity(skylight);

            Assert.IsTrue(world.ColumnObstructsMovement(8, 8, 1), "The skylight blocks movement.");
            Assert.AreEqual(0.0, world.ColumnViewOpacity(8, 8, 1), 1e-9, "The skylight does not block sight.");
        }

        [Test]
        public void IsPassable_Grounded_MatchesPassableTerrain()
        {
            var world = MakeWorld();
            var passable = new WorldLocation(5, 5, 0);
            var blocked = new WorldLocation(6, 5, 0);
            world.SetTerrain("Indoors", passable);
            world.SetTerrain("Mountain", blocked);

            var grounded = new Character(); // no Flight component

            Assert.AreEqual(world.PassableTerrain(passable), world.IsPassable(passable, grounded));
            Assert.AreEqual(world.PassableTerrain(blocked), world.IsPassable(blocked, grounded));
            Assert.IsTrue(world.IsPassable(passable, grounded));
            Assert.IsFalse(world.IsPassable(blocked, grounded));
        }

        [Test]
        public void IsPassable_AirborneFlyer_IgnoresGroundObstruction_AboveWallHeight()
        {
            var world = MakeWorld();
            world.SetTerrain("Mountain", new WorldLocation(7, 7, 0)); // impassable, height = DefaultWallHeight

            var flyer = new Character();
            flyer.Set(new Flight { MinBand = 1, MaxBand = 5, CruiseBand = 2, State = FlightState.Airborne });

            int h = world.DefaultWallHeight;

            Assert.IsFalse(world.IsPassable(new WorldLocation(7, 7, h - 1), flyer),
                "A flyer at a band still inside the wall is blocked.");
            Assert.IsTrue(world.IsPassable(new WorldLocation(7, 7, h), flyer),
                "A flyer above the wall passes over impassable ground.");
        }

        [Test]
        public void IsPassable_AirborneFlyer_OutsideBandRange_IsBlocked()
        {
            var world = MakeWorld();
            var flyer = new Character();
            flyer.Set(new Flight { MinBand = 1, MaxBand = 5, State = FlightState.Airborne });

            Assert.IsFalse(world.IsPassable(new WorldLocation(7, 7, 6), flyer), "Above MaxBand is blocked.");
            Assert.IsFalse(world.IsPassable(new WorldLocation(7, 7, 0), flyer), "Below MinBand (ground) is blocked while airborne.");
            Assert.IsTrue(world.IsPassable(new WorldLocation(7, 7, 2), flyer), "A clear band within range is passable.");
        }
    }
}
