using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;

namespace Aetherium.WorldGen.Generators.Outdoor
{
    /// <summary>A settlement the planner placed — its core cell, tier, the entity it created, and the
    /// site facts (coastal, founding biome) the economy layer keys production off.</summary>
    public sealed record PlacedSettlement(
        WorldLocation Center, SettlementTier Tier, SettlementEntity Entity, bool Coastal, string Biome);

    /// <summary>Per-tier placement recipe: how many, how far apart (great-circle radians), how big the
    /// core, and the nominal population.</summary>
    public sealed record SettlementTierSpec(
        SettlementTier Tier, int Count, double MinSeparationRadians, int CoreRadius, int Population);

    /// <summary>
    /// Places settlements on the sphere in tiers — a few far-flung capitals down to many close-packed
    /// villages — the way the square <c>OverworldGenerator</c> picks city sites, but sphere-native:
    /// sites are spaced by <b>great-circle distance</b> (H3 X/Y can't be subtracted) and the candidate
    /// pool is real buildable lowland read back from the terrain the biome pass committed.
    ///
    /// <para>Two biases shape a believable map: sites lean <b>coastal</b> (a mild preference, so ports and
    /// river towns dominate without emptying the interior), and bigger tiers are placed <b>first</b> so
    /// capitals claim well-separated ground before villages fill in around them. Each site becomes a
    /// persistent <see cref="SettlementEntity"/> carrying a <see cref="Settlement"/> — the hook the
    /// economy and transport layers attach to — with a built-up core stamped into the terrain.</para>
    /// </summary>
    public sealed class H3SettlementPlanner
    {
        private static readonly IGridTopology Topo = H3Topology.Instance;

        // Terrain a settlement may be founded on. Forest is included (it can be cleared); Water and
        // Mountain are not.
        private static readonly HashSet<string> Buildable =
            new(StringComparer.Ordinal) { "Plains", "Hills", "Desert", "Forest" };

        // How strongly siting leans toward the coast: subtracted from a candidate's random sort key when
        // it borders water, so coastal sites tend earlier without crowding out every inland site.
        private const double CoastalBias = 0.35;

        public IReadOnlyList<PlacedSettlement> Plan(
            World world, IReadOnlyList<WorldLocation> landCells,
            IReadOnlyList<SettlementTierSpec> tiers, Random rng)
        {
            // Candidate = buildable lowland cell, tagged coastal if it borders water, with a random sort
            // key nudged toward the coast. Sorting ascending by key gives coastal-leaning-but-varied order.
            var candidates = new List<(WorldLocation Loc, LatLng Center, bool Coastal, double Key)>();
            foreach (var loc in landCells)
            {
                var name = world.GetTerrainType(loc)?.Name;
                if (name is null || !Buildable.Contains(name)) continue;
                bool coastal = BordersWater(world, loc);
                double key = rng.NextDouble() - (coastal ? CoastalBias : 0.0);
                candidates.Add((loc, H3SphereGeo.Center(loc), coastal, key));
            }
            candidates.Sort((a, b) => a.Key.CompareTo(b.Key));

            var placed = new List<PlacedSettlement>();
            var placedCenters = new List<LatLng>();

            foreach (var spec in tiers)
            {
                if (spec.Count <= 0) continue;
                int placedThisTier = 0;
                int ordinal = 1;
                foreach (var cand in candidates)
                {
                    if (placedThisTier >= spec.Count) break;

                    // The founding biome must still be here — a larger tier's core may have paved it.
                    var biome = world.GetTerrainType(cand.Loc)?.Name;
                    if (biome is null || !Buildable.Contains(biome)) continue;

                    if (TooCloseToExisting(cand.Center, placedCenters, spec.MinSeparationRadians)) continue;

                    var entity = new SettlementEntity();
                    entity.Set(new WorldLocation(cand.Loc.X, cand.Loc.Y, cand.Loc.Z));
                    entity.Set(new Settlement
                    {
                        Name = $"{spec.Tier} {ordinal}",
                        Tier = spec.Tier,
                        Population = spec.Population,
                        Biome = biome,
                        CoreRadius = spec.CoreRadius,
                        Coastal = cand.Coastal,
                    });
                    world.AddEntity(entity);
                    StampCore(world, cand.Loc, spec.CoreRadius);

                    placed.Add(new PlacedSettlement(cand.Loc, spec.Tier, entity, cand.Coastal, biome));
                    placedCenters.Add(cand.Center);
                    placedThisTier++;
                    ordinal++;
                }
            }
            return placed;
        }

        private static bool BordersWater(World world, WorldLocation loc)
        {
            foreach (var n in Topo.Neighbors(H3SphereGeo.ToCoord(loc)))
                if (world.GetTerrainType(H3SphereGeo.ToLoc(n))?.Name == "Water")
                    return true;
            return false;
        }

        private static bool TooCloseToExisting(LatLng center, List<LatLng> placed, double minSep)
        {
            foreach (var p in placed)
                if (H3SphereGeo.GreatCircleRadians(center, p) < minSep)
                    return true;
            return false;
        }

        // Stamp a built-up core: interior Indoors (buildings/streets), outer ring Road (the town edge a
        // highway meets). Radius 0 is a single Indoors cell — a hamlet.
        private static void StampCore(World world, WorldLocation center, int radius)
        {
            var origin = H3SphereGeo.ToH3(center);
            foreach (var d in origin.GridDiskDistances(Math.Max(0, radius)))
            {
                var loc = H3SphereGeo.ToLoc(H3Topology.FromH3((ulong)d.Index, center.Z));
                world.SetTerrain(radius > 0 && d.Distance == radius ? "Road" : "Indoors", loc);
            }
        }
    }
}
