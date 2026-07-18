using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Topology;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Sphere-native rivers. Where the square generator traced a target-biased random walk, on the
    /// sphere we do the real thing: <b>steepest descent</b> down the elevation field that
    /// <see cref="H3TerrainGenerator"/> already sampled per cell. A river starts at a high, dry
    /// headwater and follows its lowest neighbour every step until it reaches the sea (a Water cell or
    /// a cell below sea level) or pools in a local minimum. Headwaters are chosen from the highest land
    /// and spaced apart so rivers don't bunch on one massif.
    ///
    /// <para>Rivers <b>widen downstream</b> — a trickle at the source, a broad multi-lane channel at the
    /// mouth — by stamping a growing <c>gridDisk</c> around each step. That width is deliberate: it is
    /// what lets wide vehicles pass each other in either direction on the water, several lanes abreast,
    /// near the coast where traffic is heaviest. A river is carved as <c>Water</c> (the palette has no
    /// separate river type); the returned cell set lets the caller treat those channels as navigable
    /// transport corridors and lets settlement siting prefer a riverbank.</para>
    /// </summary>
    public sealed class H3RiverCarver
    {
        private static readonly IGridTopology Topo = H3Topology.Instance;

        /// <param name="elevation">Per-cell elevation for the whole shell (same field used for biomes).</param>
        /// <param name="seaLevel">Elevation cut-off below which a cell is sea.</param>
        /// <param name="riverCount">How many rivers to trace.</param>
        /// <param name="sourceWidthRadius">Channel half-width at the headwater (0 = a single cell).</param>
        /// <param name="mouthWidthRadius">Maximum channel half-width, approached near the sea.</param>
        /// <param name="widenEveryNSteps">Steps between each one-cell increase in half-width.</param>
        /// <returns>Every cell carved to Water by a river (for riverbank siting and transport).</returns>
        public IReadOnlyCollection<WorldLocation> Carve(
            World world,
            IReadOnlyDictionary<WorldLocation, double> elevation,
            double seaLevel,
            int riverCount,
            int sourceWidthRadius,
            int mouthWidthRadius,
            int widenEveryNSteps,
            Random rng)
        {
            var carved = new HashSet<WorldLocation>();
            if (riverCount <= 0 || elevation.Count == 0) return carved;
            widenEveryNSteps = Math.Max(1, widenEveryNSteps);

            var sources = ChooseHeadwaters(elevation, seaLevel, riverCount, rng);
            // A river visits few cells relative to the shell; a shared step budget stops a pathological
            // descent (a long flat plateau with micro-jitter) from ever running away.
            int maxStepsPerRiver = Math.Max(64, elevation.Count / 8);

            foreach (var source in sources)
            {
                var path = Descend(world, elevation, seaLevel, source, maxStepsPerRiver);
                for (int i = 0; i < path.Count; i++)
                {
                    int radius = Math.Min(mouthWidthRadius, sourceWidthRadius + i / widenEveryNSteps);
                    StampChannel(world, path[i], radius, carved);
                }
            }
            return carved;
        }

        // Highest land cells, greedily thinned so no two chosen headwaters sit within a minimum
        // great-circle separation — otherwise every river would spawn on the single tallest massif.
        private static List<WorldLocation> ChooseHeadwaters(
            IReadOnlyDictionary<WorldLocation, double> elevation, double seaLevel, int count, Random rng)
        {
            var land = elevation.Where(kv => kv.Value >= seaLevel).ToList();
            if (land.Count == 0) return new List<WorldLocation>();

            // Consider only the upper elevation band as candidate sources; jitter the sort key a hair so
            // ties (and near-ties on a smooth field) don't always resolve the same way for a given seed.
            var candidates = land
                .OrderByDescending(kv => kv.Value + (rng.NextDouble() - 0.5) * 1e-6)
                .Take(Math.Max(count * 8, 32))
                .Select(kv => kv.Key)
                .ToList();

            // Separation shrinks if the candidate pool is small (low-res test worlds) so we can still
            // place the asked-for count.
            double minSep = 0.15;
            var chosen = new List<WorldLocation>();
            var chosenCenters = new List<H3.Model.LatLng>();
            for (int attempt = 0; attempt < 4 && chosen.Count < count; attempt++)
            {
                foreach (var c in candidates)
                {
                    if (chosen.Count >= count) break;
                    if (chosen.Contains(c)) continue;
                    var cc = H3SphereGeo.Center(c);
                    bool tooClose = chosenCenters.Any(o => H3SphereGeo.GreatCircleRadians(cc, o) < minSep);
                    if (tooClose) continue;
                    chosen.Add(c);
                    chosenCenters.Add(cc);
                }
                minSep *= 0.5; // relax and sweep again if we came up short
            }
            return chosen;
        }

        // Follow the lowest unvisited neighbour each step. Stop at the sea (Water terrain or a cell below
        // sea level, which is included as the river mouth) or at a local minimum (an inland lake).
        private static List<WorldLocation> Descend(
            World world, IReadOnlyDictionary<WorldLocation, double> elevation,
            double seaLevel, WorldLocation source, int maxSteps)
        {
            var path = new List<WorldLocation>();
            var visited = new HashSet<WorldLocation>();
            var current = source;

            for (int step = 0; step < maxSteps; step++)
            {
                path.Add(current);
                visited.Add(current);

                // Already merged into existing water (sea or a river carved earlier this pass).
                if (world.GetTerrainType(current)?.Name == "Water") break;

                WorldLocation? lowest = null;
                double lowestElev = elevation.TryGetValue(current, out var ce) ? ce : double.MaxValue;
                foreach (var n in Topo.Neighbors(H3SphereGeo.ToCoord(current)))
                {
                    var nl = H3SphereGeo.ToLoc(n);
                    if (visited.Contains(nl)) continue;
                    if (!elevation.TryGetValue(nl, out var ne)) continue;
                    if (ne < lowestElev) { lowestElev = ne; lowest = nl; }
                }

                if (lowest is null) break;                 // local minimum — the river pools here
                if (lowestElev < seaLevel) { path.Add(lowest); break; } // reached the sea; include the mouth
                current = lowest;
            }
            return path;
        }

        // Overwrite a disc of the given half-width with Water, recording the cells. Only land is carved;
        // cells already Water are left as-is (and not re-recorded) so overlapping mouths stay cheap.
        private static void StampChannel(World world, WorldLocation center, int radius, HashSet<WorldLocation> carved)
        {
            foreach (var cell in Topo.Range(H3SphereGeo.ToCoord(center), Math.Max(0, radius)))
            {
                var loc = H3SphereGeo.ToLoc(cell);
                if (world.GetTerrainType(loc)?.Name == "Water") continue;
                world.SetTerrain("Water", loc);
                carved.Add(loc);
            }
        }
    }
}
