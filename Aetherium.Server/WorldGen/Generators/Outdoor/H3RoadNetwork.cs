using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Topology;
using Aetherium.WorldGen.Algorithms.Graphs;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>A carved road between two settlements. <see cref="Highway"/> marks the wide inter-city
    /// trunk routes; <see cref="Length"/> is the corridor's cell count (a natural travel-time proxy for
    /// the economy layer).</summary>
    public sealed record RoadEdge(PlacedSettlement A, PlacedSettlement B, bool Highway, int Length);

    /// <summary>
    /// Connects settlements with a sphere-native road network. The graph is a <b>minimum spanning tree</b>
    /// (every settlement reachable, no wasted road) plus each settlement's <b>k nearest neighbours</b>, so
    /// the network has loops and alternate routes rather than a brittle single-path tree — exactly what an
    /// economy needs to reroute trade around a blockage. Edges are weighted by great-circle distance and
    /// carved along H3's own <c>gridPathCells</c>, so a road follows the shortest surface path on the
    /// sphere, pentagons and all.
    ///
    /// <para>Roads are <b>wide corridors</b>, not one-cell tracks: a trunk highway between cities is stamped
    /// several cells across so wide vehicles pass each other in both directions, several lanes abreast; feeder
    /// roads to villages are narrower. A corridor <b>bridges water</b> — it plows straight over a river or
    /// bay as a causeway (overwriting Water with Road) rather than detouring, matching the square generator's
    /// behaviour. The returned edge list is the transport graph the economy layer moves goods along.</para>
    /// </summary>
    public sealed class H3RoadNetwork
    {
        private static readonly IGridTopology Topo = H3Topology.Instance;

        /// <param name="extraNearestNeighbors">k — extra nearest-neighbour edges per settlement, on top of the MST.</param>
        /// <param name="highwayWidthRadius">Half-width of a trunk highway (either endpoint a City/Capital).</param>
        /// <param name="roadWidthRadius">Half-width of a feeder road.</param>
        public IReadOnlyList<RoadEdge> Connect(
            World world, IReadOnlyList<PlacedSettlement> settlements,
            int extraNearestNeighbors, int highwayWidthRadius, int roadWidthRadius)
        {
            var edges = new List<RoadEdge>();
            if (settlements.Count < 2) return edges;

            var centers = settlements.Select(s => H3SphereGeo.Center(s.Center)).ToList();
            double Dist(int a, int b) => H3SphereGeo.GreatCircleRadians(centers[a], centers[b]);

            // Undirected edge set, keyed by ordered index pair so MST and k-NN don't carve a road twice.
            var pairs = new HashSet<(int, int)>();
            void AddPair(int a, int b)
            {
                if (a == b) return;
                pairs.Add(a < b ? (a, b) : (b, a));
            }

            // Backbone: MST over the settlements. Node.Data carries the settlement index; the weight func
            // uses great-circle distance so the tree is geodesic, not based on the meaningless packed X/Y.
            var nodes = settlements.Select((_, i) => new MinimumSpanningTree.Node(i, 0, i)).ToList();
            foreach (var e in MinimumSpanningTree.ComputeMST(nodes, (x, y) => Dist((int)x.Data!, (int)y.Data!)))
                AddPair((int)e.From.Data!, (int)e.To.Data!);

            // Redundancy: each settlement's k nearest neighbours (loops + alternate trade routes).
            int k = Math.Max(0, extraNearestNeighbors);
            if (k > 0)
            {
                for (int i = 0; i < settlements.Count; i++)
                {
                    foreach (var j in Enumerable.Range(0, settlements.Count)
                                 .Where(j => j != i)
                                 .OrderBy(j => Dist(i, j))
                                 .Take(k))
                        AddPair(i, j);
                }
            }

            // Carve each edge. A shared "already road" set keeps overlapping corridors from re-stamping.
            var carved = new HashSet<WorldLocation>();
            foreach (var (a, b) in pairs)
            {
                bool highway = settlements[a].Tier >= SettlementTier.City || settlements[b].Tier >= SettlementTier.City;
                int width = highway ? highwayWidthRadius : roadWidthRadius;
                int length = CarveCorridor(world, settlements[a].Center, settlements[b].Center, width, carved);
                edges.Add(new RoadEdge(settlements[a], settlements[b], highway, length));
            }
            return edges;
        }

        // Trace the geodesic cell path A→B and stamp a disc of the given half-width as Road along it,
        // bridging water. Returns the number of path cells (corridor length).
        private static int CarveCorridor(World world, WorldLocation a, WorldLocation b, int width, HashSet<WorldLocation> carved)
        {
            int length = 0;
            foreach (var pathCell in Topo.Line(H3SphereGeo.ToCoord(a), H3SphereGeo.ToCoord(b)))
            {
                length++;
                foreach (var cell in Topo.Range(pathCell, Math.Max(0, width)))
                {
                    var loc = H3SphereGeo.ToLoc(cell);
                    if (!carved.Add(loc)) continue;     // already paved this pass
                    if (world.GetTerrainType(loc)?.Name == "Road") continue;
                    world.SetTerrain("Road", loc);
                }
            }
            return length;
        }
    }
}
