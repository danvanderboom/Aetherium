using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen.Algorithms.Noise;
using H3;
using H3.Extensions;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Sphere-native terrain generator (docs/h3-topology.md): a whole planet as a shell of H3 cells
    /// at a chosen resolution, rather than a Width×Height rectangle. Terrain is classified from two
    /// 3-D fractal-noise fields — elevation and moisture — sampled over each cell's <b>centre unit
    /// vector</b> on the sphere. Sampling in 3-D (not by latitude/longitude) is what makes it
    /// seamless: there is no date-line discontinuity and no pole singularity, and the 12 pentagons
    /// are just ordinary cells to the noise. The biome thresholds are shared with
    /// <see cref="OverworldGenerator"/> so a square continent and an H3 planet read as the same world
    /// with the same palette (Water / Desert / Plains / Forest / Hills / Mountain).
    ///
    /// <para>Resolution is the <c>h3Resolution</c> generator parameter (default 4 ≈ 288k cells — a
    /// planet with room for many settlements, trade routes, and transport networks). Cell count grows
    /// ~7× per resolution, so tests run this at a low resolution and the sample bundle at 4.</para>
    ///
    /// <para>Requires a world whose topology is <c>"h3"</c>. This generator sets
    /// <see cref="World.Topology"/> so downstream passes that consult it operate on the sphere.</para>
    ///
    /// <para>After the biome pass it lays down sphere-native <b>rivers</b> (steepest descent down the
    /// elevation field, widening toward the sea — <see cref="H3RiverCarver"/>), <b>settlements</b>
    /// (tiered, great-circle-spaced, coastal-leaning — <see cref="H3SettlementPlanner"/>), and the wide
    /// <b>road</b> corridors between them (MST + nearest-neighbour, bridging water —
    /// <see cref="H3RoadNetwork"/>). The square-grid feature passes stay gated off for H3
    /// (<see cref="ITopologyAwareGenerator"/> + the pass <c>SupportsTopology</c> gate); these are their
    /// sphere-native replacements. Connectivity validation and the economy layer are the next follow-up.</para>
    /// </summary>
    public class H3TerrainGenerator : IMapGenerator, ITopologyAwareGenerator
    {
        public IReadOnlyCollection<string> SupportedTopologies { get; } = new[] { "h3" };

        // Every biome share is chosen by PERCENTILE of the actual noise field, not by fixed cut-offs.
        // A square map made oceans with a border falloff (drown the edges); a sphere has no edge, so
        // instead we pick a global sea level that submerges a target fraction of the planet — which is
        // exactly how a planet's ocean coverage is a design knob — and split the land and the lowland
        // band the same way. Percentiles also make biome variety robust to the noise distribution
        // (raw 3-D fractal noise clusters near 0.5, which fixed thresholds would collapse to one biome).
        // Defaults ≈ Earth-ish: ~55% ocean; of the land, the highest tenth is mountains and the next
        // fifth is hills; of the lowland, the driest/wettest thirds are desert/forest.
        private const double DefaultOceanFraction = 0.55;
        private const double DefaultMountainLandFraction = 0.10;
        private const double DefaultHillLandFraction = 0.20;
        private const double DefaultDesertLowlandFraction = 0.30;
        private const double DefaultForestLowlandFraction = 0.30;

        private readonly OverworldWorldBuilder _palette = new();

        public World Generate(GeneratorContext context)
        {
            // The generator owns the tiling: it emits packed H3 cell indices as WorldLocations, so
            // the world it returns is an H3 world regardless of how it was invoked.
            var world = new World { Topology = H3Topology.Instance };
            var tileTypes = _palette.TileTypes;
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(_palette.CreateTerrainTypes(tileTypes));

            // Vertical extent + perception slab (z-altitude). Defaults keep the surface single-Z; a bundle
            // opts into subway bands below (minBand), orbit room above (maxBand), and how far up/down
            // perception reaches (slab depth) so a surface player glimpses a tunnel mouth or a flyer above.
            world.MinBand = context.GetIntParam("minBand", world.MinBand, -256, 0);
            world.MaxBand = context.GetIntParam("maxBand", world.MaxBand, 1, 4096);
            world.SlabDepthBelow = context.GetIntParam("slabDepthBelow", world.SlabDepthBelow, 0, 64);
            world.SlabDepthAbove = context.GetIntParam("slabDepthAbove", world.SlabDepthAbove, 0, 64);
            world.BandLabels[-2] = "subway";
            world.BandLabels[0] = "surface";
            world.BandLabels[3] = "skyway";

            // Resolution 4 ≈ 288k cells. Capped at 6 (~12M cells) so a mis-set parameter can't try to
            // allocate a hundred million tile entities; raise deliberately if a game needs finer.
            int resolution = context.GetIntParam("h3Resolution", 4, min: 0, max: 6);
            int z = context.ZLevel;

            // Feature scale on the unit sphere: the domain here is a unit vector (|v| = 1), so a
            // scale of ~1.5–2.5 gives continent-sized landmasses. Independent of resolution.
            double elevScale = context.GetDoubleParam("elevScale", 1.7, 0.05, 100.0);
            double moistScale = context.GetDoubleParam("moistScale", 2.4, 0.05, 100.0);
            int octaves = context.GetIntParam("octaves", 5, 1, 8);

            double oceanFraction = context.GetDoubleParam("oceanFraction", DefaultOceanFraction, 0.0, 0.95);
            double mountainLandFraction = context.GetDoubleParam("mountainLandFraction", DefaultMountainLandFraction, 0.0, 1.0);
            double hillLandFraction = context.GetDoubleParam("hillLandFraction", DefaultHillLandFraction, 0.0, 1.0);
            double desertLowlandFraction = context.GetDoubleParam("desertLowlandFraction", DefaultDesertLowlandFraction, 0.0, 1.0);
            double forestLowlandFraction = context.GetDoubleParam("forestLowlandFraction", DefaultForestLowlandFraction, 0.0, 1.0);

            var elevNoise = new PerlinNoise(context.EffectiveSeed);
            // A distinct, seed-derived stream for moisture so the two fields aren't correlated.
            var moistNoise = new PerlinNoise(context.GetRandom("h3-moisture").Next());

            // Pass 1: sample elevation + moisture for every cell of the shell. Enumerate the whole
            // sphere by expanding each resolution-0 base cell to its descendants at the target
            // resolution — GetChildrenForResolution yields 7 children per hexagon and 6 per pentagon,
            // so the union is exactly the H3 cell set at that resolution.
            var locs = new List<WorldLocation>();
            var elevs = new List<double>();
            var moists = new List<double>();
            // Elevation kept keyed by cell so the river carver can walk the field downhill later.
            var elevationByLoc = new Dictionary<WorldLocation, double>();
            foreach (var baseCell in H3Index.GetRes0Cells())
            {
                foreach (var cell in baseCell.GetChildrenForResolution(resolution))
                {
                    var center = cell.ToLatLng();
                    // Cell-centre unit vector (seamless 3-D noise domain).
                    double lat = center.Latitude, lon = center.Longitude;
                    double cosLat = Math.Cos(lat);
                    double ux = cosLat * Math.Cos(lon);
                    double uy = cosLat * Math.Sin(lon);
                    double uz = Math.Sin(lat);

                    double e = elevNoise.FractalNoiseNormalized(ux * elevScale, uy * elevScale, uz * elevScale, octaves, 0.5, 2.0);
                    double m = moistNoise.FractalNoiseNormalized(ux * moistScale, uy * moistScale, uz * moistScale, octaves, 0.5, 2.0);

                    var coord = H3Topology.FromH3((ulong)cell, z);
                    var wl = new WorldLocation(coord.X, coord.Y, z);
                    locs.Add(wl);
                    elevs.Add(e);
                    moists.Add(m);
                    elevationByLoc[wl] = e;
                }
            }

            // Choose sea level and relief thresholds by percentile of the real field, so ocean
            // coverage and mountain/hill share are controllable regardless of the noise distribution.
            double seaThreshold = Percentile(elevs, oceanFraction);
            var landElevs = elevs.Where(e => e >= seaThreshold).ToList();
            double mountainThreshold = Percentile(landElevs, 1.0 - mountainLandFraction);
            double hillThreshold = Percentile(landElevs, 1.0 - mountainLandFraction - hillLandFraction);

            // Moisture cut-offs are percentiles of the lowland cells' moisture, so desert and forest
            // always get real area regardless of how the noise clustered.
            var lowlandMoist = new List<double>();
            for (int i = 0; i < elevs.Count; i++)
                if (elevs[i] >= seaThreshold && elevs[i] < hillThreshold)
                    lowlandMoist.Add(moists[i]);
            double dryThreshold = Percentile(lowlandMoist, desertLowlandFraction);
            double wetThreshold = Percentile(lowlandMoist, 1.0 - forestLowlandFraction);

            // Pass 2: classify and commit terrain. Collect the land cells (everything not sea) as the
            // universe the settlement planner draws buildable sites from.
            WorldLocation? firstCell = null, firstPassable = null, firstPlains = null;
            var biomeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var landCells = new List<WorldLocation>();
            for (int i = 0; i < locs.Count; i++)
            {
                string terrainName = Classify(elevs[i], moists[i], seaThreshold, hillThreshold, mountainThreshold, dryThreshold, wetThreshold);
                var loc = locs[i];
                world.SetTerrain(terrainName, loc);

                biomeCounts[terrainName] = biomeCounts.TryGetValue(terrainName, out var c) ? c + 1 : 1;
                if (terrainName != "Water") landCells.Add(loc);
                firstCell ??= loc;
                if (world.PassableTerrain(loc))
                {
                    firstPassable ??= loc;
                    if (terrainName == "Plains")
                        firstPlains ??= loc;
                }
            }

            // Spawn on open ground: prefer Plains, then any passable cell, then whatever exists.
            context.StartLocation = firstPlains ?? firstPassable ?? firstCell
                ?? new WorldLocation(0, 0, z);

            // Record biome coverage (fraction of the shell) for telemetry / difficulty profiling.
            if (locs.Count > 0)
                foreach (var kv in biomeCounts)
                    context.Metrics.RecordBiomeCoverage(kv.Key, (double)kv.Value / locs.Count);

            // ---- Sphere-native features: rivers, then settlements, then the roads between them ----
            // Mean edge length (great-circle radians) at this resolution, so settlement spacing can be
            // expressed in "cells apart" and stay meaningful whether the shell is res-2 or res-6.
            double edgeRadians = MeanEdgeRadians(locs);
            int landCount = landCells.Count;

            // Rivers first — a natural feature the settlements then site against.
            int riverCount = context.GetIntParam("riverCount", ScaledCount(landCount, 3500, 3, 60), 0, 500);
            int riverSourceWidth = context.GetIntParam("riverSourceWidth", 0, 0, 6);
            int riverMouthWidth = context.GetIntParam("riverMouthWidth", 2, 0, 8);
            int riverWidenSteps = context.GetIntParam("riverWidenSteps", 12, 1, 10000);
            var riverCells = new H3RiverCarver().Carve(
                world, elevationByLoc, seaThreshold,
                riverCount, riverSourceWidth, riverMouthWidth, riverWidenSteps,
                context.GetRandom("h3-rivers"));

            // Settlements, tiered. Spacing given in cells → radians via the mean edge length.
            var tiers = new List<SettlementTierSpec>
            {
                new(SettlementTier.Capital,
                    context.GetIntParam("capitalCount", ScaledCount(landCount, 40000, 2, 10), 0, 100),
                    context.GetDoubleParam("capitalSpacingCells", 90, 0, 1_000_000) * edgeRadians, 3, 1_000_000),
                new(SettlementTier.City,
                    context.GetIntParam("cityCount", ScaledCount(landCount, 10000, 3, 40), 0, 500),
                    context.GetDoubleParam("citySpacingCells", 45, 0, 1_000_000) * edgeRadians, 2, 200_000),
                new(SettlementTier.Town,
                    context.GetIntParam("townCount", ScaledCount(landCount, 3500, 6, 120), 0, 2000),
                    context.GetDoubleParam("townSpacingCells", 20, 0, 1_000_000) * edgeRadians, 1, 30_000),
                new(SettlementTier.Village,
                    context.GetIntParam("villageCount", ScaledCount(landCount, 1200, 10, 300), 0, 5000),
                    context.GetDoubleParam("villageSpacingCells", 10, 0, 1_000_000) * edgeRadians, 0, 3_000),
            };
            var settlements = new H3SettlementPlanner().Plan(
                world, landCells, tiers, context.GetRandom("h3-settlements"));

            // Roads: MST backbone + k nearest, wide corridors that bridge water.
            int roadNeighbors = context.GetIntParam("roadNeighbors", 2, 0, 8);
            int highwayWidth = context.GetIntParam("highwayWidth", 2, 0, 8);
            int roadWidth = context.GetIntParam("roadWidth", 1, 0, 8);
            var roads = new H3RoadNetwork().Connect(world, settlements, roadNeighbors, highwayWidth, roadWidth);

            // Seed the economy onto the settlements: producers/consumers/markets from biome + population,
            // and trade links from the road graph. From here the map's EconomySystem drives the numbers.
            foreach (var ps in settlements)
                Aetherium.Server.Economy.EconomySeeder.Seed(ps.Entity, ps.Entity.Get<Settlement>());
            foreach (var edge in roads)
                Aetherium.Server.Economy.EconomySeeder.Link(edge.A.Entity, edge.B.Entity, edge.Highway, edge.Length);

            // Spawn at the capital if one was placed (the natural starting city); otherwise keep the
            // open-ground fallback chosen above.
            var capital = settlements.FirstOrDefault(s => s.Tier == SettlementTier.Capital)
                          ?? settlements.FirstOrDefault();
            if (capital is not null)
                context.StartLocation = capital.Center;

            // Feature telemetry.
            if (locs.Count > 0)
                context.Metrics.RecordBiomeCoverage("River", (double)riverCells.Count / locs.Count);
            context.Metrics.SetMetric("settlements", settlements.Count);
            context.Metrics.SetMetric("capitals", settlements.Count(s => s.Tier == SettlementTier.Capital));
            context.Metrics.SetMetric("cities", settlements.Count(s => s.Tier == SettlementTier.City));
            context.Metrics.SetMetric("towns", settlements.Count(s => s.Tier == SettlementTier.Town));
            context.Metrics.SetMetric("villages", settlements.Count(s => s.Tier == SettlementTier.Village));
            context.Metrics.SetMetric("roads", roads.Count);

            // Satellites (opt-in; default 0 so plain H3 worlds keep their exact cell count). Each rides its
            // own high band far above the perception slab — out of normal sight, radio-only — so they never
            // collide however much their orbits criss-cross.
            int satelliteCount = context.GetIntParam("satelliteCount", 0, 0, 512);
            if (satelliteCount > 0)
            {
                int satBaseBand = context.GetIntParam("satelliteBaseBand", Math.Max(world.SlabDepthAbove + 4, 8), 1, world.MaxBand);
                int satBandGap = context.GetIntParam("satelliteBandGap", 2, 1, 64);
                int satMinRadius = context.GetIntParam("satelliteMinRadius", 20, 1, 1000);
                int satMaxRadius = context.GetIntParam("satelliteMaxRadius", 60, 1, 2000);
                int satMinPeriod = context.GetIntParam("satelliteMinPeriod", 1, 1, 100);
                int satMaxPeriod = context.GetIntParam("satelliteMaxPeriod", 4, 1, 100);
                var sats = H3SatelliteSeeder.Seed(world, satelliteCount, satBaseBand, satBandGap,
                    satMinRadius, satMaxRadius, satMinPeriod, satMaxPeriod, locs, context.GetRandom("h3-satellites"));
                context.Metrics.SetMetric("satellites", sats.Count);
            }

            // A sensible default light at spawn so an ambient-lit session isn't pitch black (outdoor
            // play uses sunlight; a carried lamp handles interiors) — mirrors the planar generators.
            var light = new LightEntity();
            light.Set(new LightSource(1.0, 50));
            light.Set(context.StartLocation);
            world.AddEntity(light);

            return world;
        }

        private static string Classify(double e, double m, double sea, double hill, double mountain, double dry, double wet)
        {
            if (e < sea) return "Water";
            if (e >= mountain) return "Mountain";
            if (e >= hill) return "Hills";
            // Lowland band: moisture decides desert / plains / forest.
            if (m < dry) return "Desert";
            if (m < wet) return "Plains";
            return "Forest";
        }

        // A per-tier settlement count that scales with the amount of land, clamped to a sane range so a
        // tiny test shell and a res-6 planet both get a believable spread.
        private static int ScaledCount(int landCount, int cellsPer, int min, int max)
            => Math.Clamp(landCount / Math.Max(1, cellsPer), min, max);

        // Mean cell edge length in great-circle radians, sampled across the shell. Lets feature spacing
        // be expressed in cells and converted to the sphere's own metric at any resolution.
        private static double MeanEdgeRadians(IReadOnlyList<WorldLocation> locs)
        {
            if (locs.Count == 0) return 0.02;
            var topo = H3Topology.Instance;
            double sum = 0; int n = 0;
            int stride = Math.Max(1, locs.Count / 5);
            for (int i = 0; i < locs.Count && n < 30; i += stride)
            {
                foreach (var nb in topo.Neighbors(H3SphereGeo.ToCoord(locs[i])))
                {
                    sum += H3SphereGeo.GreatCircleRadians(locs[i], H3SphereGeo.ToLoc(nb));
                    n++;
                }
            }
            return n > 0 ? sum / n : 0.02;
        }

        // The value at fraction f of a set, by rank (f in [0,1]). Used to turn "submerge 55% of the
        // planet" into a concrete elevation cut-off on the actual field. Sorts a copy, so the caller's
        // list is untouched; O(n log n) over the cells, negligible beside noise sampling.
        private static double Percentile(IReadOnlyList<double> values, double f)
        {
            if (values.Count == 0) return 0.0;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            int idx = (int)Math.Round(Math.Clamp(f, 0.0, 1.0) * (sorted.Length - 1));
            return sorted[idx];
        }
    }
}
