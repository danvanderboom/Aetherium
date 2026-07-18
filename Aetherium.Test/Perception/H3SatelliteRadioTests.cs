using System;
using System.Collections.Generic;
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

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// The radio-gated orbital channel (docs/design/h3-sphere-worldgen.md P4.5): satellites are invisible to
    /// the naked eye and appear in perception only while the viewer carries (or is) an active, tuned radio,
    /// which surfaces those overhead — high relative Z — and offers a hack affordance in range. Verifies the
    /// gate, the reveal (self radio and carried-item radio), the range cutoff, and that off/untuned is silent.
    /// </summary>
    [TestFixture]
    public class H3SatelliteRadioTests
    {
        [Test]
        public void SatellitesAreInvisibleWithoutARadio()
        {
            var world = WorldWithSatellite(out var player, out var sat);
            var p = Perceive(world, player, self: null);

            Assert.That(p.VisibleCharacters.Any(c => c.Id == sat.EntityId), Is.False,
                "no radio → the satellite is undetectable");
            Assert.That(p.Affordances.Any(a => a.Action == "hack"), Is.False);
        }

        [Test]
        public void ATunedRadioRevealsTheSatelliteOverheadAndOffersAHack()
        {
            var world = WorldWithSatellite(out var player, out var sat, satBand: 24);
            var self = Perceiver(player, new RadioReceiver { SatelliteRange = 48 });

            var p = Perceive(world, player, self);

            var seen = p.VisibleCharacters.FirstOrDefault(c => c.Id == sat.EntityId);
            Assert.That(seen, Is.Not.Null, "a tuned radio opens the orbital channel");
            Assert.That(seen!.Location!.Z, Is.EqualTo(24), "the satellite reads high overhead (relative Z = its band)");
            Assert.That(seen.Name, Is.EqualTo("satellite"));
            Assert.That(p.Affordances.Any(a => a.Action == "hack" && a.TargetId == sat.EntityId), Is.True,
                "an overhead satellite can be hacked");
        }

        [Test]
        public void ASatelliteOutOfRadioRangeIsNotReceived()
        {
            var world = WorldWithSatellite(out var player, out var sat, satBand: 24, groundTrackOffset: 20);
            var self = Perceiver(player, new RadioReceiver { SatelliteRange = 5 }); // it's 20 cells away

            var p = Perceive(world, player, self);
            Assert.That(p.VisibleCharacters.Any(c => c.Id == sat.EntityId), Is.False,
                "a receiver only picks up satellites near its zenith");
        }

        [Test]
        public void AnOffOrUntunedRadioIsSilent()
        {
            var world = WorldWithSatellite(out var player, out var sat);

            var off = Perceive(world, player, Perceiver(player, new RadioReceiver { On = false }));
            Assert.That(off.VisibleCharacters.Any(c => c.Id == sat.EntityId), Is.False, "powered off → silent");

            var untuned = Perceive(world, player, Perceiver(player, new RadioReceiver { Tuned = false }));
            Assert.That(untuned.VisibleCharacters.Any(c => c.Id == sat.EntityId), Is.False, "off-frequency → silent");
        }

        [Test]
        public void ACarriedRadioItemOpensTheChannel()
        {
            var world = WorldWithSatellite(out var player, out var sat);

            // The radio is an item in the player's pack, not an innate sense.
            var radio = new RadioItem();
            radio.Set(new WorldLocation(player.X, player.Y, player.Z));
            world.AddEntity(radio);

            var character = new Character();
            character.Set(new WorldLocation(player.X, player.Y, player.Z));
            var inv = new Inventory();
            inv.TryAdd(radio.EntityId, radio);
            character.Set(inv);
            world.AddEntity(character);

            var p = Perceive(world, player, self: character);
            Assert.That(p.VisibleCharacters.Any(c => c.Id == sat.EntityId), Is.True,
                "carrying an active radio item reveals the satellite");
        }

        // ---- helpers ----

        private static Aetherium.Model.PerceptionDto Perceive(Aetherium.Core.World world, WorldLocation loc, Entity? self)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                world, loc, Aetherium.WorldDirection.North, new System.Drawing.Size(16, 16), self);

        private static Entity Perceiver(WorldLocation at, RadioReceiver radio)
        {
            var e = new Character();
            e.Set(new WorldLocation(at.X, at.Y, at.Z));
            e.Set(radio);
            return e;
        }

        // An H3 world with one satellite at band satBand, its ground track over the player (offset 0) or
        // groundTrackOffset cells away. The satellite carries the same components the seeder gives it.
        private static Aetherium.Core.World WorldWithSatellite(
            out WorldLocation player, out SatelliteEntity sat, int satBand = 24, int groundTrackOffset = 0)
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

            var trackIdx = groundTrackOffset <= 0
                ? centerIdx
                : centerIdx.GridDiskDistances(groundTrackOffset).First(d => d.Distance == groundTrackOffset).Index;
            var satLoc = Loc(trackIdx, satBand);

            sat = new SatelliteEntity();
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
