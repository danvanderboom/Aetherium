using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Topology;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Verifies the sphere-native H3 terrain generator (docs/design/h3-sphere-worldgen.md): it
    /// terraforms the <b>entire</b> spherical shell at a chosen H3 resolution (hexagons + the 12
    /// pentagons), classifies every cell into a known biome from 3-D noise, is deterministic per
    /// seed, and spawns on open ground. Runs at a low resolution for speed; the count math is the
    /// exact H3 cell count 2 + 120·7^res, so a full-shell assertion doubles as a coverage check.
    /// </summary>
    [TestFixture]
    public class H3TerrainGeneratorTests
    {
        private const int Seed = 20260718;

        // Biomes plus the feature terrain the generator now paints on top of them (settlement cores are
        // Road/Indoors; rivers are Water). Every cell must carry one of these — never null or unknown.
        private static readonly HashSet<string> KnownBiomes = new(StringComparer.Ordinal)
        {
            "Water", "Plains", "Forest", "Desert", "Hills", "Mountain", "Road", "Indoors"
        };

        private static long ExpectedCellCount(int resolution)
            => 2 + 120 * (long)Math.Pow(7, resolution);

        private static (Aetherium.Core.World World, GeneratorContext Context) Generate(
            int resolution = 2, int seed = Seed)
        {
            var context = new GeneratorContext(256, 256, seed)
            {
                GeneratorParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = resolution.ToString()
                }
            };
            var world = new H3TerrainGenerator().Generate(context);
            return (world, context);
        }

        [Test]
        public void DeclaresH3OnlySupport()
        {
            Assert.That(new H3TerrainGenerator().SupportedTopologies, Is.EquivalentTo(new[] { "h3" }));
        }

        [Test]
        public void GeneratesTheEntireSphericalShell()
        {
            var (world, _) = Generate(resolution: 2);

            // Every H3 cell at the resolution becomes exactly one terrain-bearing location. The
            // start-light shares the spawn cell's bucket, so distinct locations == cell count.
            Assert.That(world.EntitiesByLocation.Keys.Count, Is.EqualTo(ExpectedCellCount(2)));
            Assert.That(world.Topology, Is.SameAs(H3Topology.Instance),
                "the generator owns the tiling — the world it returns must be an H3 world");
        }

        [Test]
        public void IncludesTheTwelvePentagonBaseCellsAtResolutionZero()
        {
            // Resolution 0 is exactly the 122 base cells (110 hexagons + 12 pentagons); a full shell
            // there proves pentagons are enumerated and packed like any other cell.
            var (world, _) = Generate(resolution: 0);
            Assert.That(world.EntitiesByLocation.Keys.Count, Is.EqualTo(122));

            int pentagons = world.EntitiesByLocation.Keys.Count(loc =>
                H3Topology.Instance.DirectionCount(new GridCoord(loc.X, loc.Y, loc.Z)) == 5);
            Assert.That(pentagons, Is.EqualTo(12), "a sphere tiled with hexagons has exactly 12 pentagons");
        }

        [Test]
        public void EveryCellCarriesAKnownBiome()
        {
            var (world, _) = Generate();
            foreach (var loc in world.EntitiesByLocation.Keys)
            {
                var name = world.GetTerrainType(loc)?.Name;
                Assert.That(name, Is.Not.Null, $"cell {loc} has no terrain");
                Assert.That(KnownBiomes, Does.Contain(name), $"cell {loc} has unknown biome '{name}'");
            }
        }

        [Test]
        public void ProducesOceansLandAndBiomeVariety()
        {
            var (world, _) = Generate();
            var biomes = world.EntitiesByLocation.Keys
                .Select(l => world.GetTerrainType(l)?.Name)
                .Where(n => n != null)
                .Distinct()
                .ToHashSet();

            Assert.That(biomes, Does.Contain("Water"), "a planet needs oceans");
            Assert.That(biomes.Any(b => b != "Water"), Is.True, "a planet needs land");
            // Elevation + moisture should carve more than a two-tone world.
            Assert.That(biomes.Count, Is.GreaterThanOrEqualTo(3), "expected varied biomes, got: " + string.Join(",", biomes));
        }

        [Test]
        public void SameSeedIsDeterministic()
        {
            var (a, _) = Generate(seed: 42);
            var (b, _) = Generate(seed: 42);
            Assert.That(Fingerprint(a), Is.EqualTo(Fingerprint(b)));
        }

        [Test]
        public void DifferentSeedProducesADifferentPlanet()
        {
            var (a, _) = Generate(seed: 1);
            var (b, _) = Generate(seed: 2);
            Assert.That(Fingerprint(a), Is.Not.EqualTo(Fingerprint(b)),
                "distinct seeds must not collapse to the same planet");
        }

        [Test]
        public void SpawnsOnPassableGround()
        {
            var (world, context) = Generate();
            Assert.That(context.StartLocation, Is.Not.Null);
            Assert.That(world.PassableTerrain(context.StartLocation!), Is.True,
                "the spawn must be walkable, not ocean or mountain");
        }

        [Test]
        public void SpawnCellNeighboursAreRealCellsOfTheShell()
        {
            // The spawn's H3 neighbours (via the topology's own adjacency) must themselves be cells
            // present in the world — proof that the packed X/Y round-trip through H3 neighbour math.
            var (world, context) = Generate();
            var start = new GridCoord(context.StartLocation!.X, context.StartLocation.Y, context.StartLocation.Z);

            var neighbours = H3Topology.Instance.Neighbors(start).ToList();
            Assert.That(neighbours.Count, Is.GreaterThanOrEqualTo(5));
            foreach (var n in neighbours)
                Assert.That(world.EntitiesByLocation.ContainsKey(new WorldLocation(n.X, n.Y, n.Z)), Is.True,
                    $"neighbour {n} of the spawn is not a cell in the shell");
        }

        [Test]
        public void PlacesALightAtTheSpawn()
        {
            var (world, context) = Generate();
            bool lit = world.Entities.Values.Any(e =>
                e.Has<LightSource>() && e.Has<WorldLocation>() && e.Get<WorldLocation>().Equals(context.StartLocation));
            Assert.That(lit, Is.True, "the spawn carries a default light so an ambient session isn't black");
        }

        private static string Fingerprint(Aetherium.Core.World world) => string.Join(";",
            world.EntitiesByLocation.Keys
                .OrderBy(l => l.X).ThenBy(l => l.Y)
                .Select(l => $"{l.X},{l.Y}:{world.GetTerrainType(l)?.Name}"));
    }
}
