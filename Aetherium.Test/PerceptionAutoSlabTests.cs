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
    /// Section 5.1 of add-adaptive-depth-visualization: adaptive slab depth. When AutoSlab is on the emitted
    /// depth expands toward the configured budget to cover occupied bands near the viewer and collapses to
    /// single-Z over flat terrain — always bounded by SlabDepthBelow/Above and SlabDepthCap. Because the slab
    /// loop already emits only content cells, adaptive depth is behaviorally transparent (same visible cells as
    /// a fixed budget); it only avoids scanning empty far bands. Default off keeps the fixed configured depth.
    /// </summary>
    public class PerceptionAutoSlabTests
    {
        private sealed class TestObstacle : Entity { }

        private static World MakeSlabWorld(int below = 4, int above = 4, int cap = 8)
        {
            var world = new World();
            var builder = new WorldBuilders.TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            world.SlabDepthBelow = below;
            world.SlabDepthAbove = above;
            world.SlabDepthCap = cap;
            return world;
        }

        private static void AddEntityAt(World world, WorldLocation at)
        {
            var flyer = new Character();
            flyer.Set(at);
            world.AddEntity(flyer);
        }

        private static void AddOpaque(World world, WorldLocation at)
        {
            var e = new TestObstacle();
            e.Set(at);
            e.Set(new ObstructsView { Opacity = 1.0, Height = 1 });
            world.AddEntity(e);
        }

        private static PerceptionDto Perceive(World world, WorldLocation player) =>
            new PerceptionService().ComputePerception(world, player, WorldDirection.North, new Size(42, 22));

        // --- EffectiveSlabDepth: the adaptive-depth logic in isolation ---

        [Test]
        public void EffectiveSlabDepth_AutoSlabOff_ReturnsConfiguredClampedDepth()
        {
            var world = MakeSlabWorld(below: 4, above: 10, cap: 8); // AutoSlab default off
            AddEntityAt(world, new WorldLocation(20, 20, 2));       // content present but ignored when off

            var (below, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(4, below, "Below returns the configured budget when AutoSlab is off.");
            Assert.AreEqual(8, above, "Above is the configured budget clamped to the cap when AutoSlab is off.");
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_FlatColumn_CollapsesToZero()
        {
            var world = MakeSlabWorld();
            world.AutoSlab = true; // nothing off-focus anywhere

            var (below, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(0, below);
            Assert.AreEqual(0, above);
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_ExpandsToFurthestOccupiedBandAbove()
        {
            var world = MakeSlabWorld(above: 5);
            world.AutoSlab = true;
            world.AutoSlabProbeRadius = 0;
            AddEntityAt(world, new WorldLocation(20, 20, 1)); // nearer content
            AddEntityAt(world, new WorldLocation(20, 20, 3)); // furthest content within budget

            var (below, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(0, below, "Nothing below → collapses below.");
            Assert.AreEqual(3, above, "Expands to the furthest occupied band above.");
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_ExpandsToFurthestOccupiedBandBelow()
        {
            var world = MakeSlabWorld(below: 5);
            world.AutoSlab = true;
            world.AutoSlabProbeRadius = 0;
            world.SetTerrain("Indoors", new WorldLocation(20, 20, -2));

            var (below, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(2, below);
            Assert.AreEqual(0, above);
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_IgnoresContentBeyondBudget()
        {
            var world = MakeSlabWorld(above: 2); // budget only reaches +2
            world.AutoSlab = true;
            world.AutoSlabProbeRadius = 0;
            AddEntityAt(world, new WorldLocation(20, 20, 4)); // content beyond the budget

            var (_, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(0, above, "Content past the configured budget does not expand the slab.");
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_ProbeRadius_DetectsAdjacentColumn()
        {
            var world = MakeSlabWorld(above: 4);
            world.AutoSlab = true;
            AddEntityAt(world, new WorldLocation(21, 20, 3)); // one cell east, three bands up

            world.AutoSlabProbeRadius = 0;
            Assert.AreEqual(0, world.EffectiveSlabDepth(20, 20, 0).above,
                "Radius 0 only probes the viewer's own column.");

            world.AutoSlabProbeRadius = 1;
            Assert.AreEqual(3, world.EffectiveSlabDepth(20, 20, 0).above,
                "Radius 1 catches an interchange the viewer stands beside.");
        }

        [Test]
        public void EffectiveSlabDepth_AutoSlab_ClampedToCap()
        {
            var world = MakeSlabWorld(above: 10, cap: 3);
            world.AutoSlab = true;
            world.AutoSlabProbeRadius = 0;
            AddEntityAt(world, new WorldLocation(20, 20, 5)); // beyond the cap

            var (_, above) = world.EffectiveSlabDepth(20, 20, 0);

            Assert.AreEqual(0, above, "The cap bounds the probe; content past it is not covered.");
        }

        // --- Perception equivalence: adaptive depth drops no content ---

        [Test]
        public void AutoSlab_On_SeesSameContentCellsAsFixedDepth()
        {
            var player = new WorldLocation(20, 20, 0);

            var fixedWorld = MakeSlabWorld();
            AddEntityAt(fixedWorld, new WorldLocation(20, 20, 3));
            fixedWorld.SetTerrain("Indoors", new WorldLocation(20, 20, -2));
            var fixedPerception = Perceive(fixedWorld, player);

            var autoWorld = MakeSlabWorld();
            autoWorld.AutoSlab = true;
            AddEntityAt(autoWorld, new WorldLocation(20, 20, 3));
            autoWorld.SetTerrain("Indoors", new WorldLocation(20, 20, -2));
            var autoPerception = Perceive(autoWorld, player);

            Assert.IsTrue(autoPerception.Visuals.ContainsKey("0,0,3"), "Adaptive depth still covers the overhead flyer.");
            Assert.IsTrue(autoPerception.Visuals.ContainsKey("0,0,-2"), "Adaptive depth still covers the level below.");
            Assert.AreEqual(
                fixedPerception.Visuals.ContainsKey("0,0,3"),
                autoPerception.Visuals.ContainsKey("0,0,3"),
                "Adaptive and fixed depth agree on the overhead cell.");
            Assert.AreEqual(
                fixedPerception.Visuals.ContainsKey("0,0,-2"),
                autoPerception.Visuals.ContainsKey("0,0,-2"),
                "Adaptive and fixed depth agree on the below cell.");
        }

        [Test]
        public void AutoSlab_On_PreservesOcclusion()
        {
            var world = MakeSlabWorld();
            world.AutoSlab = true;
            var player = new WorldLocation(20, 20, 0);
            AddOpaque(world, new WorldLocation(20, 20, 2)); // opaque bridge
            AddEntityAt(world, new WorldLocation(20, 20, 3)); // flyer above the bridge

            var perception = Perceive(world, player);

            Assert.IsFalse(perception.Visuals.ContainsKey("0,0,3"), "Occlusion still hides the flyer under adaptive depth.");
            Assert.IsTrue(perception.Visuals.ContainsKey("0,0,2"), "The bridge underside remains visible.");
        }
    }
}
