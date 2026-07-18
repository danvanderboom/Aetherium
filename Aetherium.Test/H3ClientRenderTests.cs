extern alias Console;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Flying;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using H3;
using H3.Algorithms;
using H3.Extensions;
using ClientConsoleMapView = Console::Aetherium.Views.ClientConsoleMapView;

namespace Aetherium.Test
{
    /// <summary>
    /// End-to-end: the real H3 perception feeds the real console depth renderer. Confirms the sphere's
    /// z-altitude content reaches the client — a radio-detected satellite occupies a high band in the level
    /// ribbon (and is absent without the radio), and a subway underfoot shows through an opening. The
    /// depth renderer is topology-agnostic (it composites by relative Z), so proving the H3 DTO drives it is
    /// the client half of "nail z-altitude on server, perception, and client".
    /// </summary>
    public class H3ClientRenderTests
    {
        [Test]
        public void ARadioDetectedSatelliteOccupiesAHighBandInTheClientRibbon()
        {
            var world = WorldWithSatelliteOverhead(out var player, satBand: 24);
            var radioSelf = Perceiver(player, new RadioReceiver { SatelliteRange = 48 });

            // With the radio: the satellite's band shows in the ribbon the client builds from the frame.
            var withRadio = Render(Perceive(world, player, radioSelf));
            Assert.That(withRadio.BuildLevelRibbon().Any(b => b.Item1 == 24), Is.True,
                "a detected satellite reads as a contact high overhead");

            // Without a radio: the sky is empty, so no high band appears.
            var noRadio = Render(Perceive(world, player, self: null));
            Assert.That(noRadio.BuildLevelRibbon().Any(b => b.Item1 == 24), Is.False,
                "no radio → the satellite never reaches the client");
        }

        [Test]
        public void ASubwayUnderfootShowsThroughToTheClient()
        {
            var world = WorldWithSatelliteOverhead(out var player, satBand: 24);
            world.SlabDepthBelow = 3;
            // Carve a subway two bands under the player and open the cell above it so the composite falls through.
            var below = new WorldLocation(player.X, player.Y, -2);
            world.SetTerrain("Subway", below);

            var view = Render(Perceive(world, player, self: null));
            // The subway band is occupied in the ribbon (perceived through the slab).
            Assert.That(view.BuildLevelRibbon().Any(b => b.Item1 == -2), Is.True,
                "the subway underfoot reaches the client on its negative band");
        }

        // ---- helpers ----

        private static Aetherium.Model.PerceptionDto Perceive(Aetherium.Core.World world, WorldLocation loc, Entity? self)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                world, loc, Aetherium.WorldDirection.North, new Size(20, 10), self);

        private static ClientConsoleMapView Render(Aetherium.Model.PerceptionDto p)
        {
            var view = new ClientConsoleMapView(new Point(0, 0), new Size(20, 10), hasFrame: false)
            {
                Perception = p,
                WorldLocation = p.PlayerLocation,
            };
            view.CaptureRenderedFrame();
            return view;
        }

        private static Entity Perceiver(WorldLocation at, RadioReceiver radio)
        {
            var e = new Character();
            e.Set(new WorldLocation(at.X, at.Y, at.Z));
            e.Set(radio);
            return e;
        }

        private static Aetherium.Core.World WorldWithSatelliteOverhead(out WorldLocation player, int satBand)
        {
            var world = new Aetherium.Core.World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            world.MinBand = -4;
            world.MaxBand = 64;

            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            foreach (var d in centerIdx.GridDiskDistances(6))
                world.SetTerrain("Plains", Loc(d.Index, 0));
            player = Loc(centerIdx, 0);

            var satLoc = Loc(centerIdx, satBand);
            var sat = new SatelliteEntity();
            sat.Set(satLoc);
            sat.Set(new OrbitPath { Ring = new List<WorldLocation> { satLoc }, Cursor = 0 });
            sat.Set(new Flight { MinBand = satBand, MaxBand = 64, CruiseBand = satBand });
            sat.Set(FlyerProfiles.Satellite());
            sat.Set(new CreatureTypeTag("satellite"));
            world.AddEntity(sat);

            return world;
        }

        private static WorldLocation Loc(H3Index idx, int band)
        {
            var gc = H3Topology.FromH3((ulong)idx, band);
            return new WorldLocation(gc.X, gc.Y, band);
        }
    }
}
