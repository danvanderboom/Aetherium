using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;
using H3;
using H3.Algorithms;
using H3.Extensions;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Sphere-native rivers, settlements, and roads (docs/design/h3-sphere-worldgen.md). The integration
    /// fixture generates one planet and asserts the whole feature stack landed: tiered settlements as
    /// persistent entities, a capital spawn, built-up cores, and carved rivers/roads. The unit fixtures
    /// drive the river carver and road network directly on hand-built neighbourhoods so the downhill,
    /// widening, connectivity, and water-bridging behaviours are pinned precisely.
    /// </summary>
    [TestFixture]
    public class H3SphereFeaturesTests
    {
        private Aetherium.Core.World _world = null!;
        private GeneratorContext _context = null!;

        [OneTimeSetUp]
        public void GenerateOnePlanet()
        {
            _context = new GeneratorContext(256, 256, 20260718)
            {
                GeneratorParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = "2",
                    ["capitalCount"] = "2",
                    ["cityCount"] = "3",
                    ["townCount"] = "6",
                    ["villageCount"] = "16",
                    ["capitalSpacingCells"] = "12",
                    ["citySpacingCells"] = "8",
                    ["townSpacingCells"] = "5",
                    ["villageSpacingCells"] = "3",
                    ["riverCount"] = "6",
                    ["riverMouthWidth"] = "2",
                    ["roadNeighbors"] = "2",
                    ["highwayWidth"] = "2",
                    ["roadWidth"] = "1",
                }
            };
            _world = new H3TerrainGenerator().Generate(_context);
        }

        private List<Settlement> Settlements() => _world.Entities.Values
            .Where(e => e.Has<Settlement>())
            .Select(e => e.Get<Settlement>())
            .ToList();

        [Test]
        public void PlacesManySettlementsAsPersistentEntities()
        {
            var settlements = Settlements();
            Assert.That(settlements.Count, Is.GreaterThan(10),
                "a planet with room should carry many settlements");
            foreach (var s in settlements)
            {
                Assert.That(s.Name, Is.Not.Empty);
                Assert.That(s.Population, Is.GreaterThan(0));
                Assert.That(new[] { "Plains", "Hills", "Desert", "Forest" }, Does.Contain(s.Biome),
                    "a settlement is founded on buildable lowland");
            }
        }

        [Test]
        public void PlacesTheFullTierHierarchy()
        {
            var tiers = Settlements().Select(s => s.Tier).ToHashSet();
            // The big tiers are few and can lose the odd site to spacing, but a capital and villages must land.
            Assert.That(tiers, Does.Contain(SettlementTier.Capital), "the planet needs at least one capital");
            Assert.That(tiers, Does.Contain(SettlementTier.Village), "the planet needs villages");
            // Core radius must scale with tier (capital widest).
            foreach (var s in Settlements())
                Assert.That(s.CoreRadius, Is.EqualTo(s.Tier switch
                {
                    SettlementTier.Capital => 3,
                    SettlementTier.City => 2,
                    SettlementTier.Town => 1,
                    _ => 0,
                }));
        }

        [Test]
        public void SpawnsAtACapital()
        {
            var capitals = _world.Entities.Values
                .Where(e => e.Has<Settlement>() && e.Get<Settlement>().Tier == SettlementTier.Capital)
                .Select(e => e.Get<WorldLocation>())
                .ToList();
            Assert.That(capitals, Is.Not.Empty);
            Assert.That(capitals.Any(c => c.Equals(_context.StartLocation)), Is.True,
                "the spawn should be one of the capitals");
        }

        [Test]
        public void CapitalCoresAreBuiltUp()
        {
            var capital = _world.Entities.Values
                .First(e => e.Has<Settlement>() && e.Get<Settlement>().Tier == SettlementTier.Capital)
                .Get<WorldLocation>();
            // Built-up = Indoors (buildings) or Road (a trunk route carved through the centre) — never raw biome.
            Assert.That(_world.GetTerrainType(capital)?.Name, Is.AnyOf("Indoors", "Road"),
                "a settlement centre is built-up, not raw biome");
            // The core disc means neighbours are paved too (Indoors interior / Road ring).
            var built = H3Topology.Instance.Neighbors(new GridCoord(capital.X, capital.Y, capital.Z))
                .Select(n => _world.GetTerrainType(new WorldLocation(n.X, n.Y, n.Z))?.Name)
                .Count(t => t is "Indoors" or "Road");
            Assert.That(built, Is.GreaterThan(0), "the core should extend past the centre cell");
        }

        [Test]
        public void CarvesRiversAndRoads()
        {
            Assert.That(_context.Metrics.TryGetMetric("roads", out var roads), Is.True);
            Assert.That(roads, Is.GreaterThan(0), "settlements must be connected by roads");
            Assert.That(_context.Metrics.BiomeCoverage.TryGetValue("River", out var river), Is.True);
            Assert.That(river, Is.GreaterThan(0.0), "rivers must have carved some channel");

            // Roads exist on the map as passable corridors.
            bool anyRoad = _world.EntitiesByLocation.Keys.Any(l => _world.GetTerrainType(l)?.Name == "Road");
            Assert.That(anyRoad, Is.True);
        }

        // ---- unit: river carver ----

        [Test]
        public void RiverFlowsDownhillToTheSeaAsWater()
        {
            var (world, center, disk) = MakeGradientWorld(radius: 7, seaFromDistance: 6);
            var elevation = disk.ToDictionary(c => c.Loc, c => c.Elev);
            double seaLevel = SeaLevelForDistance(6, 7);

            var carved = new H3RiverCarver().Carve(
                world, elevation, seaLevel,
                riverCount: 1, sourceWidthRadius: 0, mouthWidthRadius: 0, widenEveryNSteps: 100,
                new Random(1));

            Assert.That(carved, Is.Not.Empty, "a river should carve at least one channel cell");
            foreach (var c in carved)
                Assert.That(world.GetTerrainType(c)?.Name, Is.EqualTo("Water"), "a river is carved as Water");

            // The channel must reach the sea: some carved cell borders a pre-existing ocean cell.
            bool reachedSea = carved.Any(c => H3Topology.Instance
                .Neighbors(new GridCoord(c.X, c.Y, c.Z))
                .Any(n => IsPreExistingSea(disk, new WorldLocation(n.X, n.Y, n.Z), seaLevel)));
            Assert.That(reachedSea, Is.True, "the river should descend all the way to the sea");
        }

        [Test]
        public void RiverWidensDownstream()
        {
            var (world, center, disk) = MakeGradientWorld(radius: 8, seaFromDistance: 7);
            var elevation = disk.ToDictionary(c => c.Loc, c => c.Elev);
            double seaLevel = SeaLevelForDistance(7, 8);

            var narrow = new H3RiverCarver().Carve(world, elevation, seaLevel,
                1, 0, 0, 100, new Random(1));           // never widens

            var (world2, _, disk2) = MakeGradientWorld(radius: 8, seaFromDistance: 7);
            var elevation2 = disk2.ToDictionary(c => c.Loc, c => c.Elev);
            var wide = new H3RiverCarver().Carve(world2, elevation2, seaLevel,
                1, 0, 3, 2, new Random(1));             // widens every 2 steps up to radius 3

            Assert.That(wide.Count, Is.GreaterThan(narrow.Count),
                "a downstream-widening river must carve more cells than a one-cell channel");
        }

        // ---- unit: road network ----

        [Test]
        public void RoadConnectsSettlementsAndBridgesWater()
        {
            var world = MakeFlatWorld(radius: 8, out var center, out var disk);
            // Two settlements on opposite rims of the disk.
            var a = disk.OrderByDescending(c => c.Distance).First(c => c.Distance == disk.Max(x => x.Distance)).Loc;
            var b = disk.OrderByDescending(c => Gc(a, c.Loc)).First().Loc;  // farthest from a by great-circle

            // Put a Water stripe on the geodesic so the road has to bridge it.
            var line = H3Topology.Instance.Line(new GridCoord(a.X, a.Y, a.Z), new GridCoord(b.X, b.Y, b.Z))
                .Select(g => new WorldLocation(g.X, g.Y, g.Z)).ToList();
            var mid = line[line.Count / 2];
            world.SetTerrain("Water", mid);
            Assert.That(world.GetTerrainType(mid)?.Name, Is.EqualTo("Water"), "precondition: the line crosses water");

            var settlements = new List<PlacedSettlement>
            {
                new(a, SettlementTier.Village, new SettlementEntity(), false, "Plains"),
                new(b, SettlementTier.Village, new SettlementEntity(), false, "Plains"),
            };
            var edges = new H3RoadNetwork().Connect(world, settlements,
                extraNearestNeighbors: 1, highwayWidthRadius: 1, roadWidthRadius: 0);

            Assert.That(edges.Count, Is.EqualTo(1), "two settlements → a single de-duplicated edge");
            foreach (var cell in line)
                Assert.That(world.GetTerrainType(cell)?.Name, Is.EqualTo("Road"),
                    $"every cell on the route should be paved; {cell} was not");
            Assert.That(world.GetTerrainType(mid)?.Name, Is.EqualTo("Road"),
                "the road must bridge the water it crosses");
        }

        [Test]
        public void RoadMstConnectsEverySettlement()
        {
            var world = MakeFlatWorld(radius: 8, out var center, out var disk);
            // Five spread-out settlements.
            var picks = new[] { 0, 2, 4, 6, 8 }
                .Select(d => disk.First(c => c.Distance == d).Loc)
                .Distinct()
                .ToList();
            var settlements = picks
                .Select(p => new PlacedSettlement(p, SettlementTier.Town, new SettlementEntity(), false, "Plains"))
                .ToList();

            var edges = new H3RoadNetwork().Connect(world, settlements,
                extraNearestNeighbors: 0, highwayWidthRadius: 0, roadWidthRadius: 0);

            Assert.That(edges.Count, Is.EqualTo(settlements.Count - 1), "an MST over n nodes has n-1 edges");

            // Union-find: the MST edges must connect all settlements into one component.
            var parent = Enumerable.Range(0, settlements.Count).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var index = settlements.Select((s, i) => (s.Center, i)).ToDictionary(t => t.Center, t => t.i);
            foreach (var e in edges) parent[Find(index[e.A.Center])] = Find(index[e.B.Center]);
            Assert.That(settlements.Select((_, i) => Find(i)).Distinct().Count(), Is.EqualTo(1),
                "every settlement must be reachable from every other");
        }

        // ---- helpers ----

        private static Aetherium.Core.World NewH3World()
        {
            var world = new Aetherium.Core.World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            return world;
        }

        // A res-3 neighbourhood with a radial elevation gradient: high at the centre, low at the rim,
        // with the outer rings below sea level (an island). Cells at/over sea are committed as Water.
        private static (Aetherium.Core.World World, WorldLocation Center, List<(WorldLocation Loc, int Distance, double Elev)> Disk)
            MakeGradientWorld(int radius, int seaFromDistance)
        {
            var world = NewH3World();
            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            var center = Loc(centerIdx);
            var disk = new List<(WorldLocation, int, double)>();
            double sea = SeaLevelForDistance(seaFromDistance, radius);
            foreach (var d in centerIdx.GridDiskDistances(radius))
            {
                var loc = Loc(d.Index);
                double elev = 1.0 - (double)d.Distance / (radius + 1); // 1.0 at centre → low at rim
                world.SetTerrain(elev < sea ? "Water" : "Plains", loc);
                disk.Add((loc, d.Distance, elev));
            }
            return (world, center, disk);
        }

        private static Aetherium.Core.World MakeFlatWorld(int radius, out WorldLocation center,
            out List<(WorldLocation Loc, int Distance)> disk)
        {
            var world = NewH3World();
            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            center = Loc(centerIdx);
            disk = new List<(WorldLocation, int)>();
            foreach (var d in centerIdx.GridDiskDistances(radius))
            {
                var loc = Loc(d.Index);
                world.SetTerrain("Plains", loc);
                disk.Add((loc, d.Distance));
            }
            return world;
        }

        private static double SeaLevelForDistance(int seaFromDistance, int radius)
            => 1.0 - (double)(seaFromDistance - 0.5) / (radius + 1);

        private static bool IsPreExistingSea(List<(WorldLocation Loc, int Distance, double Elev)> disk,
            WorldLocation loc, double seaLevel)
            => disk.Any(c => c.Loc.Equals(loc) && c.Elev < seaLevel);

        private static WorldLocation Loc(H3Index idx)
        {
            var gc = H3Topology.FromH3((ulong)idx, 0);
            return new WorldLocation(gc.X, gc.Y, gc.Z);
        }

        private static double Gc(WorldLocation a, WorldLocation b)
        {
            var la = new H3Index(H3Topology.ToH3(new GridCoord(a.X, a.Y, a.Z))).ToLatLng();
            var lb = new H3Index(H3Topology.ToH3(new GridCoord(b.X, b.Y, b.Z))).ToLatLng();
            return la.GetGreatCircleDistanceInRadians(lb);
        }
    }
}
