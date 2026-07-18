using System.Drawing;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium.Model;
using Aetherium;

namespace Aetherium.Test
{
    /// <summary>
    /// Section 1 of add-adaptive-depth-visualization: the 3D occluded perception slab. Verifies that the server
    /// computes vision across a configurable band range with a vertical line-of-sight test — a flyer overhead is
    /// visible through a clear column or a glass skylight but hidden by an opaque bridge; a level below is visible
    /// through an open grate but not through solid pavement — each included cell tagged with its relative Z, with
    /// the PerceptionDto schema unchanged. Defaults keep perception single-Z unless a world opts in.
    /// </summary>
    public class PerceptionSlabTests
    {
        // A non-terrain, non-character entity used to place explicit sight obstructions (bridges, skylights, pavement).
        private sealed class TestObstacle : Entity { }

        private static World MakeSlabWorld(int below = 4, int above = 4)
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.SlabDepthBelow = below;
            world.SlabDepthAbove = above;
            return world;
        }

        private static Character AddFlyerAt(World world, WorldLocation at)
        {
            var flyer = new Character();
            flyer.Set(at);
            world.AddEntity(flyer);
            return flyer;
        }

        private static void AddOpaque(World world, WorldLocation at)
        {
            var e = new TestObstacle();
            e.Set(at);
            e.Set(new ObstructsView { Opacity = 1.0, Height = 1 }); // stone bridge / solid pavement
            world.AddEntity(e);
        }

        private static void AddSkylight(World world, WorldLocation at)
        {
            var e = new TestObstacle();
            e.Set(at);
            e.Set(new ObstructsMovement { Obstruction = 1, Height = 1 }); // blocks movement
            e.Set(new ObstructsView { Opacity = 0.0, Height = 1 });       // but transparent to sight
            world.AddEntity(e);
        }

        private static PerceptionDto Perceive(World world, WorldLocation player) =>
            new PerceptionService().ComputePerception(world, player, WorldDirection.North, new Size(42, 22));

        [Test]
        public void FlyerOverhead_ClearColumn_IsVisible_WithPositiveRelativeZ()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            AddFlyerAt(world, new WorldLocation(20, 20, 3)); // directly overhead, clear air between

            var perception = Perceive(world, player);

            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,3"), "The overhead flyer is visible through the clear column.");
        }

        [Test]
        public void FlyerOverhead_OpaqueBridgeBetween_IsHidden_BridgeVisibleInstead()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            AddOpaque(world, new WorldLocation(20, 20, 2));  // stone bridge between viewer and flyer
            AddFlyerAt(world, new WorldLocation(20, 20, 3));

            var perception = Perceive(world, player);

            Assert.IsFalse(perception.Visuals.ContainsKey("0,0,3"), "The flyer is occluded by the opaque bridge.");
            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,2"), "The bridge underside is the visible cell for that column.");
        }

        [Test]
        public void FlyerOverhead_TransparentSkylight_IsVisible()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            AddSkylight(world, new WorldLocation(20, 20, 2)); // blocks movement, Opacity 0
            AddFlyerAt(world, new WorldLocation(20, 20, 3));

            var perception = Perceive(world, player);

            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,3"), "The flyer is visible through the transparent skylight.");
        }

        [Test]
        public void LevelBelow_OpenGrate_IsVisible_WithNegativeRelativeZ()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            world.SetTerrain("Indoors", new WorldLocation(20, 20, -2)); // subway floor two bands down, open shaft above it

            var perception = Perceive(world, player);

            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,-2"), "The level below is visible down the open column.");
        }

        [Test]
        public void LevelBelow_SolidPavement_IsHidden()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            AddOpaque(world, new WorldLocation(20, 20, -1));            // solid pavement immediately below
            world.SetTerrain("Indoors", new WorldLocation(20, 20, -2)); // subway floor beneath the pavement

            var perception = Perceive(world, player);

            Assert.IsFalse(perception.Visuals.ContainsKey("0,0,-2"), "Solid pavement hides the level below.");
            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,-1"), "The pavement surface itself is visible.");
        }

        [Test]
        public void Slab_Off_ByDefault_KeepsPerceptionSingleZ()
        {
            var world = MakeSlabWorld(below: 0, above: 0); // opt-out: default depths
            var player = new WorldLocation(20, 20, 0);
            AddFlyerAt(world, new WorldLocation(20, 20, 3));

            var perception = Perceive(world, player);

            Assert.IsFalse(perception.Visuals.ContainsKey("0,0,3"), "With the slab off, only the focus band is perceived.");
        }

        [Test]
        public void DtoSchema_Unchanged_RelativeKeysAndPlayerAtOrigin()
        {
            var world = MakeSlabWorld();
            var player = new WorldLocation(20, 20, 0);
            AddFlyerAt(world, new WorldLocation(20, 20, 3));

            var perception = Perceive(world, player);

            Assert.AreEqual(0, perception.PlayerLocation.X, "Player is emitted at the relative origin.");
            Assert.AreEqual(0, perception.PlayerLocation.Y);
            Assert.AreEqual(0, perception.PlayerLocation.Z);
            var flyer = perception.Visuals["0,0,3"];
            Assert.AreEqual(3, flyer.Location.Z, "The off-focus cell carries its relative Z in the existing WorldLocationDto.");
        }
    }
}
