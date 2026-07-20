using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen.Generators.Outdoor;
using H3;
using H3.Algorithms;
using H3.Extensions;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Grade-separated transit on the sphere (docs/design/h3-sphere-worldgen.md P4): a rail backbone between
    /// the big cities on the surface, and subway tunnels a couple of bands underground from each capital.
    /// Verifies the rail MST, the underground subway band, the high-capacity trade links the economy routes
    /// over, and that a surface viewer glimpses the subway through the perception slab.
    /// </summary>
    [TestFixture]
    public class H3TransitNetworkTests
    {
        [Test]
        public void RailIsAnMstBackboneBetweenTheBigCitiesOnTheSurface()
        {
            var world = FlatWorld(out var cells);
            var s = Settlements(world, cells);
            var edges = new H3TransitNetwork().Build(world, s, railCapacity: 6, subwayCapacity: 8, subwayBand: -2);

            var rail = edges.Where(e => e.Mode == "rail").ToList();
            int majors = s.Count(x => x.Tier >= SettlementTier.City);
            Assert.That(rail.Count, Is.EqualTo(majors - 1), "rail is an MST over the City+ tier");
            Assert.That(world.EntitiesByLocation.Keys.Any(l => l.Z == 0 && world.GetTerrainType(l)?.Name == "Rail"),
                Is.True, "rail carves surface track");
        }

        [Test]
        public void RailPlacesAStationMarkerAtEveryMajorCity()
        {
            var world = FlatWorld(out var cells);
            var s = Settlements(world, cells);
            new H3TransitNetwork().Build(world, s, railCapacity: 6, subwayCapacity: 8, subwayBand: -2);

            var stations = world.EntitiesByLocation.Values
                .SelectMany(bucket => bucket.Values)
                .Where(e => e.Has<Station>())
                .Select(e => e.Get<Station>())
                .ToList();

            int majors = s.Count(x => x.Tier >= SettlementTier.City);
            Assert.That(stations.Count, Is.EqualTo(majors), "a station marks each rail stop (the City+ tier)");
            Assert.That(stations.All(st => st.LineId == "rail"), Is.True, "every marker belongs to the rail line");
            Assert.That(stations.Select(st => st.StopIndex).Distinct().Count(), Is.EqualTo(majors),
                "each station has a distinct ordinal along the line");
        }

        [Test]
        public void SubwaysRunUndergroundFromEachCapital()
        {
            var world = FlatWorld(out var cells);
            var s = Settlements(world, cells);
            var edges = new H3TransitNetwork().Build(world, s, 6, 8, subwayBand: -2);

            var subway = edges.Where(e => e.Mode == "subway").ToList();
            int capitals = s.Count(x => x.Tier == SettlementTier.Capital);
            int cities = s.Count(x => x.Tier == SettlementTier.City);
            Assert.That(subway.Count, Is.EqualTo(capitals * Math.Min(2, cities)), "each capital reaches its 2 nearest cities");

            // Subway terrain lives on the negative band, not the surface.
            Assert.That(world.EntitiesByLocation.Keys.Any(l => l.Z == -2 && world.GetTerrainType(l)?.Name == "Subway"),
                Is.True, "the subway is carved underground");
            Assert.That(world.EntitiesByLocation.Keys.Any(l => l.Z == 0 && world.GetTerrainType(l)?.Name == "Subway"),
                Is.False, "nothing subway sits on the surface band");
        }

        [Test]
        public void TransitAddsHigherCapacityTradeLinksThanRoads()
        {
            var world = FlatWorld(out var cells);
            var s = Settlements(world, cells);
            new H3TransitNetwork().Build(world, s, railCapacity: 6, subwayCapacity: 8, subwayBand: -2);

            var capital = s.First(x => x.Tier == SettlementTier.Capital).Entity;
            Assert.That(capital.Has<TradeLinks>(), Is.True);
            var links = capital.Get<TradeLinks>().Links;
            Assert.That(links.Any(l => l.Mode == "rail" && l.Capacity == 6), Is.True, "the capital is on the rail backbone");
            Assert.That(links.Any(l => l.Mode == "subway" && l.Capacity == 8), Is.True, "the capital has a subway");
            Assert.That(links.Where(l => l.Mode is "rail" or "subway").All(l => l.Capacity > 3.0), Is.True,
                "transit outclasses even a highway (capacity 3)");
        }

        [Test]
        public void ASurfaceViewerGlimpsesTheSubwayThroughTheSlab()
        {
            var world = FlatWorld(out var cells);
            world.SlabDepthBelow = 3; // opt into a downward slab
            var s = Settlements(world, cells);
            new H3TransitNetwork().Build(world, s, 6, 8, subwayBand: -2);

            // Stand on a capital (a subway runs beneath it) and look: the tunnel reads at negative relative Z.
            var capital = s.First(x => x.Tier == SettlementTier.Capital);
            var p = new Aetherium.Server.PerceptionService().ComputePerception(
                world, capital.Center, Aetherium.WorldDirection.North, new System.Drawing.Size(16, 16), null);

            bool tunnelBelow = p.Visuals.Keys.Any(k => k.EndsWith(",-2"));
            Assert.That(tunnelBelow, Is.True, "the subway underfoot is perceived through the vertical slab");
        }

        // ---- helpers ----

        private static Aetherium.Core.World FlatWorld(out List<(WorldLocation Loc, int Distance)> cells)
        {
            var world = new Aetherium.Core.World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            world.MinBand = -4;
            world.MaxBand = 8;

            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            cells = new List<(WorldLocation, int)>();
            foreach (var d in centerIdx.GridDiskDistances(14))
            {
                var gc = H3Topology.FromH3((ulong)d.Index, 0);
                var loc = new WorldLocation(gc.X, gc.Y, 0);
                world.SetTerrain("Plains", loc);
                cells.Add((loc, d.Distance));
            }
            return world;
        }

        // A capital at the centre, three cities out at increasing range, and a town (excluded from transit).
        private static List<PlacedSettlement> Settlements(Aetherium.Core.World world, List<(WorldLocation Loc, int Distance)> cells)
        {
            WorldLocation At(int dist) => cells.First(c => c.Distance == dist).Loc;
            return new List<PlacedSettlement>
            {
                Make(world, At(0), SettlementTier.Capital),
                Make(world, At(4), SettlementTier.City),
                Make(world, At(8), SettlementTier.City),
                Make(world, At(12), SettlementTier.City),
                Make(world, At(6), SettlementTier.Town),
            };
        }

        private static PlacedSettlement Make(Aetherium.Core.World world, WorldLocation loc, SettlementTier tier)
        {
            var e = new SettlementEntity();
            e.Set(new WorldLocation(loc.X, loc.Y, loc.Z));
            e.Set(new Settlement { Tier = tier, Name = tier.ToString(), Population = 1000, Biome = "Plains" });
            world.AddEntity(e);
            return new PlacedSettlement(loc, tier, e, false, "Plains");
        }
    }
}
