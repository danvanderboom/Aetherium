using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Satellites;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen.Generators.Outdoor;
using H3;
using H3.Algorithms;
using H3.Extensions;

namespace Aetherium.Test.Satellites
{
    /// <summary>
    /// Satellites on the sphere (docs/design/h3-sphere-worldgen.md P4.5): an orbit is an H3 gridRing at a
    /// fixed band, walked forever; each satellite rides its own band so they never collide however much
    /// their orbits criss-cross; and they are hackable flyers, deliberately not Characters. Verifies ring
    /// construction, orbit advancement (with per-satellite speed), the never-collide guarantee, and seeding.
    /// </summary>
    [TestFixture]
    public class SatelliteSystemTests
    {
        [Test]
        public void OrbitRingIsAClosedLoopOfAdjacentCellsAtOneBand()
        {
            var center = SurfaceCells(2)[0];
            var ring = H3Orbit.Ring(center, k: 6, band: 20);

            Assert.That(ring.Count, Is.GreaterThan(6), "a radius-6 hex ring has many cells");
            Assert.That(ring.All(c => c.Z == 20), Is.True, "every ring cell sits on the orbit's band");
            // The walk visits adjacent cells, so consecutive ring cells are H3 neighbours.
            for (int i = 0; i < ring.Count - 1; i++)
            {
                int d = H3Topology.Instance.Distance(
                    new GridCoord(ring[i].X, ring[i].Y, ring[i].Z),
                    new GridCoord(ring[i + 1].X, ring[i + 1].Y, ring[i + 1].Z));
                Assert.That(d, Is.EqualTo(1), $"ring step {i}->{i + 1} should be to an adjacent cell");
            }
        }

        [Test]
        public void SatelliteAdvancesOneCellAlongItsRingEachStep()
        {
            var world = NewWorld();
            var sat = SeedOne(world, ticksPerStep: 1);
            var orbit = sat.Get<OrbitPath>();

            var before = sat.Get<WorldLocation>();
            int cursor0 = orbit.Cursor;
            new SatelliteSystem().Step(world);
            var after = sat.Get<WorldLocation>();

            Assert.That(after, Is.EqualTo(orbit.Ring[(cursor0 + 1) % orbit.Ring.Count]), "moved to the next ring cell");
            Assert.That(after, Is.Not.EqualTo(before));
            // Location index tracks the move: present at the new cell, gone from the old.
            Assert.That(world.EntitiesByLocation[after].ContainsKey(sat.EntityId), Is.True);
            Assert.That(world.EntitiesByLocation.TryGetValue(before, out var oldBucket) && oldBucket.ContainsKey(sat.EntityId),
                Is.False, "the satellite left its old cell");
        }

        [Test]
        public void TicksPerStepPacesTheOrbit()
        {
            var world = NewWorld();
            var sat = SeedOne(world, ticksPerStep: 3);
            var orbit = sat.Get<OrbitPath>();
            int start = orbit.Cursor;
            var sys = new SatelliteSystem();

            sys.Step(world); sys.Step(world);
            Assert.That(orbit.Cursor, Is.EqualTo(start), "a period-3 satellite hasn't moved after 2 ticks");
            sys.Step(world);
            Assert.That(orbit.Cursor, Is.EqualTo((start + 1) % orbit.Ring.Count), "it advances on the 3rd tick");
        }

        [Test]
        public void SatellitesRideDistinctBandsAndNeverShareACell()
        {
            var world = NewWorld();
            var sats = H3SatelliteSeeder.Seed(world, count: 10, baseBand: 24, bandGap: 2,
                minRadius: 5, maxRadius: 12, minTicksPerStep: 1, maxTicksPerStep: 3,
                SurfaceCells(3), new Random(7));
            Assert.That(sats.Count, Is.GreaterThan(1));

            var bands = sats.Select(s => s.Get<WorldLocation>().Z).ToList();
            Assert.That(bands.Distinct().Count(), Is.EqualTo(sats.Count), "each satellite owns a distinct band");

            // Step a while; no two satellites ever occupy the same cell (distinct bands guarantee it).
            var sys = new SatelliteSystem();
            for (int t = 0; t < 40; t++)
            {
                sys.Step(world);
                var cells = sats.Select(s => s.Get<WorldLocation>()).ToList();
                Assert.That(cells.Distinct().Count(), Is.EqualTo(sats.Count), $"collision at tick {t}");
            }
        }

        [Test]
        public void SeededSatellitesAreHackableFlyersInTheRegistry()
        {
            var world = NewWorld();
            var sats = H3SatelliteSeeder.Seed(world, 6, 24, 2, 5, 10, 1, 4, SurfaceCells(3), new Random(3));
            Assert.That(sats.Count, Is.GreaterThan(0));

            foreach (var s in sats)
            {
                Assert.That(s.Has<Flight>(), Is.True);
                Assert.That(s.Has<OrbitPath>(), Is.True);
                Assert.That(s.Get<FlyerProfile>().Kind, Is.EqualTo("satellite"));
                Assert.That(s.Get<FlyerProfile>().Hackable, Is.True, "a satellite is hackable when overhead");
                Assert.That(world.Entities.ContainsKey(s.EntityId), Is.True);
            }
            var registered = SatelliteRegistry.ForWorld(world);
            Assert.That(registered.Count, Is.EqualTo(sats.Count));
        }

        [Test]
        public void NoSatellitesIsANoOp()
        {
            var world = NewWorld();
            Assert.DoesNotThrow(() => new SatelliteSystem().Step(world));
        }

        // ---- helpers ----

        private static Aetherium.Core.World NewWorld()
        {
            var world = new Aetherium.Core.World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            world.MinBand = -4;
            world.MaxBand = 64;
            return world;
        }

        private static SatelliteEntity SeedOne(Aetherium.Core.World world, int ticksPerStep)
            => (SatelliteEntity)H3SatelliteSeeder.Seed(world, 1, 24, 2, 6, 6, ticksPerStep, ticksPerStep,
                SurfaceCells(2), new Random(1)).Single();

        private static List<WorldLocation> SurfaceCells(int radius)
        {
            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            var cells = new List<WorldLocation>();
            foreach (var d in centerIdx.GridDiskDistances(radius))
            {
                var gc = H3Topology.FromH3((ulong)d.Index, 0);
                cells.Add(new WorldLocation(gc.X, gc.Y, 0));
            }
            return cells;
        }
    }
}
