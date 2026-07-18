using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Satellites;
using Aetherium.Topology;
using H3;
using H3.Algorithms;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Builds a closed orbit as an H3 gridRing at a fixed band: the cells at exactly grid-distance
    /// <c>k</c> from a centre, walked into a traversable cycle by adjacency. A satellite steps this ring
    /// forever, so it loops over the surface at altitude.
    /// </summary>
    public static class H3Orbit
    {
        public static List<WorldLocation> Ring(WorldLocation center, int k, int band)
        {
            var result = new List<WorldLocation>();
            if (k <= 0) return result;

            var centerIdx = H3SphereGeo.ToH3(center);
            var ringSet = new HashSet<ulong>();
            foreach (var d in centerIdx.GridDiskDistances(k))
                if (d.Distance == k) ringSet.Add((ulong)d.Index);
            if (ringSet.Count == 0) return result;

            // Walk neighbour-to-neighbour around the ring. Each ring cell has two ring-neighbours, so this
            // traces the loop; it stops when it can't extend (a pentagon defect leaves a short arc, which is
            // still a fine orbit).
            var visited = new HashSet<ulong>();
            ulong current = ringSet.First();
            while (true)
            {
                visited.Add(current);
                var gc = H3Topology.FromH3(current, band);
                result.Add(new WorldLocation(gc.X, gc.Y, band));

                ulong? next = null;
                foreach (var nb in Neighbors(current))
                    if (ringSet.Contains(nb) && !visited.Contains(nb)) { next = nb; break; }
                if (next is null) break;
                current = next.Value;
            }
            return result;
        }

        private static IEnumerable<ulong> Neighbors(ulong cell)
        {
            foreach (var d in new H3Index(cell).GridDiskDistances(1))
                if (d.Distance == 1) yield return (ulong)d.Index;
        }
    }

    /// <summary>
    /// Populates the sky with satellites. Each rides its <b>own band</b> — a distinct altitude level — so
    /// however much their rings criss-cross in projection they can never share a cell (the Z differs), which
    /// is the "many, criss-crossing, never colliding" guarantee for free. Bands are stacked from a base far
    /// above the perception slab up to the world ceiling, so orbit is out of normal sight and reachable only
    /// through a tuned radio. Each satellite gets a hackable <see cref="FlyerProfile"/> (uplink when overhead),
    /// a varied ring centre and radius, a random start phase, and its own speed, so the sky is busy and
    /// unsynchronised. A <see cref="SatelliteEntity"/> is deliberately not a Character (see that type).
    /// </summary>
    public static class H3SatelliteSeeder
    {
        public static IReadOnlyList<SatelliteEntity> Seed(
            World world, int count, int baseBand, int bandGap,
            int minRadius, int maxRadius, int minTicksPerStep, int maxTicksPerStep,
            IReadOnlyList<WorldLocation> centerCandidates, Random rng)
        {
            var satellites = new List<SatelliteEntity>();
            if (count <= 0 || centerCandidates.Count == 0) return satellites;
            bandGap = Math.Max(1, bandGap);

            // One distinct band per satellite. Cap the count to the bands available under the world ceiling
            // so the never-collide guarantee holds by construction.
            int availableBands = baseBand > world.MaxBand ? 0 : (world.MaxBand - baseBand) / bandGap + 1;
            count = Math.Min(count, availableBands);

            for (int i = 0; i < count; i++)
            {
                int band = baseBand + i * bandGap;
                var center = centerCandidates[rng.Next(centerCandidates.Count)];
                int radius = rng.Next(Math.Max(1, minRadius), Math.Max(minRadius, maxRadius) + 1);
                var ring = H3Orbit.Ring(center, radius, band);
                if (ring.Count < 2) continue; // degenerate ring (pentagon) — skip this slot

                int start = rng.Next(ring.Count);
                var sat = new SatelliteEntity();
                sat.Set(new WorldLocation(ring[start].X, ring[start].Y, band));
                sat.Set(new OrbitPath
                {
                    Ring = ring,
                    Cursor = start,
                    TicksPerStep = rng.Next(Math.Max(1, minTicksPerStep), Math.Max(minTicksPerStep, maxTicksPerStep) + 1),
                });
                sat.Set(new Flight
                {
                    State = FlightState.Airborne,
                    MinBand = baseBand,
                    MaxBand = world.MaxBand,
                    CruiseBand = band,
                    CanLand = false,
                });
                sat.Set(Aetherium.Server.Flying.FlyerProfiles.Satellite());
                sat.Set(new CreatureTypeTag("satellite"));
                world.AddEntity(sat);
                satellites.Add(sat);
            }

            SatelliteRegistry.Register(world, satellites);
            return satellites;
        }
    }
}
