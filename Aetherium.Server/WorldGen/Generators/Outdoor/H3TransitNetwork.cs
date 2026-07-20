using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.Economy;
using Aetherium.Topology;
using Aetherium.WorldGen.Algorithms.Graphs;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>A transit route between two settlements, in some mode (rail/subway).</summary>
    public sealed record TransitEdge(PlacedSettlement A, PlacedSettlement B, string Mode, int Length);

    /// <summary>
    /// Grade-separated transit on top of the surface road web: a high-capacity <b>rail</b> backbone between
    /// the big cities (band 0), and <b>subway</b> tunnels underground (a negative band) linking each capital
    /// to its nearest cities. Both become high-capacity <see cref="TradeLinks"/> so the economy routes bulk
    /// freight over them, and both carve their own terrain — rail on the surface, subway underfoot — so the
    /// subway is a real place a player rides, glimpsed from the surface through the perception slab. This is
    /// where the z-altitude bands earn their keep: the subway lives a couple of bands down, out of the way of
    /// the roads above it. Opt-in (the subway adds underground cells), enabled per bundle.
    /// </summary>
    public sealed class H3TransitNetwork
    {
        private static readonly IGridTopology Topo = H3Topology.Instance;

        public IReadOnlyList<TransitEdge> Build(
            World world, IReadOnlyList<PlacedSettlement> settlements,
            double railCapacity, double subwayCapacity, int subwayBand)
        {
            var edges = new List<TransitEdge>();

            // Rail: an MST backbone over the City/Capital tier — the trunk freight line between the big hubs.
            var majors = settlements.Where(s => s.Tier >= SettlementTier.City).ToList();
            if (majors.Count >= 2)
            {
                var centers = majors.Select(m => H3SphereGeo.Center(m.Center)).ToList();
                var nodes = majors.Select((_, i) => new MinimumSpanningTree.Node(i, 0, i)).ToList();
                foreach (var e in MinimumSpanningTree.ComputeMST(nodes,
                             (x, y) => H3SphereGeo.GreatCircleRadians(centers[(int)x.Data!], centers[(int)y.Data!])))
                {
                    var a = majors[(int)e.From.Data!];
                    var b = majors[(int)e.To.Data!];
                    int len = Carve(world, a.Center, b.Center, band: 0, "Rail");
                    EconomySeeder.LinkMode(a.Entity, b.Entity, "rail", railCapacity, len);
                    edges.Add(new TransitEdge(a, b, "rail", len));
                }

                // Platform markers at every rail stop, so the backbone is a visible, queryable line a
                // rideable service can be stood up over (add-transit-networks Phase 1). Non-obstructing,
                // co-located with the city core; the ordered stops mirror the majors list.
                var stops = majors
                    .Select(m => (m.Center, m.Entity.Has<Settlement>() ? m.Entity.Get<Settlement>().Name : "Station"))
                    .ToList();
                Aetherium.Server.Transit.TransitServicePlanner.PlaceStations(world, "rail", stops);
            }

            // Subway: each capital to its two nearest cities, underground on the subway band — the densest,
            // highest-capacity metro freight, grade-separated from everything on the surface.
            var capitals = settlements.Where(s => s.Tier == SettlementTier.Capital).ToList();
            var cities = settlements.Where(s => s.Tier == SettlementTier.City).ToList();
            foreach (var cap in capitals)
            {
                var capCenter = H3SphereGeo.Center(cap.Center);
                foreach (var city in cities
                             .OrderBy(c => H3SphereGeo.GreatCircleRadians(capCenter, H3SphereGeo.Center(c.Center)))
                             .Take(2))
                {
                    int len = Carve(world, cap.Center, city.Center, band: subwayBand, "Subway");
                    EconomySeeder.LinkMode(cap.Entity, city.Entity, "subway", subwayCapacity, len);
                    edges.Add(new TransitEdge(cap, city, "subway", len));
                }
            }

            return edges;
        }

        // Trace the geodesic between two settlements and stamp the given terrain along it at a fixed band.
        // At band 0 this retypes surface cells; at a negative band it creates the underground corridor.
        private static int Carve(World world, WorldLocation a, WorldLocation b, int band, string name)
        {
            int len = 0;
            foreach (var pathCell in Topo.Line(H3SphereGeo.ToCoord(a), H3SphereGeo.ToCoord(b)))
            {
                len++;
                world.SetTerrain(name, new WorldLocation(pathCell.X, pathCell.Y, band));
            }
            return len;
        }
    }
}
