using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using H3;
using H3.Algorithms;
using H3.Extensions;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// The vertical slab on the sphere (docs/design/h3-sphere-worldgen.md P4.5): when an H3 world opts into
    /// a perception slab, a viewer sees content in the bands above and below their own — a subway underfoot,
    /// a bridge or drone overhead — with correct relative Z, and vertical line-of-sight stops at the first
    /// opaque band. Default slab depth 0 keeps H3 single-Z. Also checks the flight envelope (true band)
    /// surfaces on the H3 path.
    /// </summary>
    [TestFixture]
    public class H3VerticalSlabTests
    {
        [Test]
        public void DefaultSlabKeepsPerceptionSingleZ()
        {
            var world = SlabWorld(above: 0, below: 0, out var player, out var center);
            world.SetTerrain("Road", Loc(center, 1)); // content overhead, but the slab is off

            var p = Compute(world, player);
            Assert.That(p.Visuals.Keys.All(k => k.EndsWith(",0")), Is.True,
                "with slab depth 0 every visible cell is on the viewer's own band");
        }

        [Test]
        public void SlabRevealsBandsAboveAndBelow()
        {
            var world = SlabWorld(above: 3, below: 3, out var player, out var center);
            world.SetTerrain("Road", Loc(center, 1));   // a bridge overhead
            world.SetTerrain("Road", Loc(center, -2));  // a subway underfoot (band -1 is open air)

            var p = Compute(world, player);
            Assert.That(p.Visuals.ContainsKey("0,0,1"), Is.True, "the bridge overhead is perceived at relZ +1");
            Assert.That(p.Visuals.ContainsKey("0,0,-2"), Is.True, "the subway underfoot is perceived at relZ -2");
        }

        [Test]
        public void AnOpaqueBandStopsTheVerticalRay()
        {
            var world = SlabWorld(above: 4, below: 0, out var player, out var center);
            world.SetTerrain("Mountain", Loc(center, 1)); // opaque (ObstructsView 1)
            world.SetTerrain("Road", Loc(center, 3));     // beyond the opaque band

            var p = Compute(world, player);
            Assert.That(p.Visuals.ContainsKey("0,0,1"), Is.True, "the opaque band's own surface is seen");
            Assert.That(p.Visuals.ContainsKey("0,0,3"), Is.False, "nothing beyond the first opaque band is seen");
        }

        [Test]
        public void EmptyAirBetweenContentIsASilhouetteGap()
        {
            var world = SlabWorld(above: 3, below: 0, out var player, out var center);
            world.SetTerrain("Road", Loc(center, 2)); // band +1 is left empty

            var p = Compute(world, player);
            Assert.That(p.Visuals.ContainsKey("0,0,1"), Is.False, "empty air emits no tile");
            Assert.That(p.Visuals.ContainsKey("0,0,2"), Is.True, "the content above the gap is still seen");
        }

        [Test]
        public void FlightEnvelopeSurfacesTheTrueBandForFlyers()
        {
            var world = SlabWorld(above: 0, below: 0, out _, out var center);
            var player = Loc(center, 4); // the flyer is cruising at band 4

            var flyer = new SettlementEntity();
            flyer.Set(new WorldLocation(player.X, player.Y, player.Z));
            flyer.Set(new Flight { MinBand = 1, MaxBand = 20, CruiseBand = 5 });

            var p = Compute(world, player, flyer);
            Assert.That(p.FlightEnvelope, Is.Not.Null, "a flyer gets an altitude gauge");
            Assert.That(p.FlightEnvelope!.CurrentBand, Is.EqualTo(4), "the gauge shows the true band, not the relative 0");
            Assert.That(p.FlightEnvelope.MaxBand, Is.EqualTo(20));

            // A non-flyer gets no envelope.
            var p2 = Compute(world, player, self: null);
            Assert.That(p2.FlightEnvelope, Is.Null);
        }

        // ---- helpers ----

        private static Aetherium.Model.PerceptionDto Compute(Aetherium.Core.World world, WorldLocation loc, Entity? self = null)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                world, loc, Aetherium.WorldDirection.North, new System.Drawing.Size(16, 16), self);

        private static Aetherium.Core.World SlabWorld(int above, int below, out WorldLocation player, out H3Index center)
        {
            var world = new Aetherium.Core.World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            world.SlabDepthAbove = above;
            world.SlabDepthBelow = below;
            world.MinBand = -8;
            world.MaxBand = 30;

            center = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            foreach (var d in center.GridDiskDistances(5))
                world.SetTerrain("Plains", Loc(d.Index, 0));

            player = Loc(center, 0);
            return world;
        }

        private static WorldLocation Loc(H3Index idx, int band)
        {
            var gc = H3Topology.FromH3((ulong)idx, band);
            return new WorldLocation(gc.X, gc.Y, band);
        }
    }
}
