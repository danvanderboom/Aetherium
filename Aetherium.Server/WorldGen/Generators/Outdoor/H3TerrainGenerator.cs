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
    /// <see cref="World.Topology"/> so downstream passes that consult it operate on the sphere.
    /// Rivers, coastal roads, settlements, and connectivity validation are square-grid today and are
    /// gated off for H3 (see <see cref="ITopologyAwareGenerator"/> and the pass
    /// <c>SupportsTopology</c> gate); their sphere-native replacements are the phased follow-up.</para>
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
                    locs.Add(new WorldLocation(coord.X, coord.Y, z));
                    elevs.Add(e);
                    moists.Add(m);
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

            // Pass 2: classify and commit terrain.
            WorldLocation? firstCell = null, firstPassable = null, firstPlains = null;
            var biomeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < locs.Count; i++)
            {
                string terrainName = Classify(elevs[i], moists[i], seaThreshold, hillThreshold, mountainThreshold, dryThreshold, wetThreshold);
                var loc = locs[i];
                world.SetTerrain(terrainName, loc);

                biomeCounts[terrainName] = biomeCounts.TryGetValue(terrainName, out var c) ? c + 1 : 1;
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
