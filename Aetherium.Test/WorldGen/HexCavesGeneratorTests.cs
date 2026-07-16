using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Topology;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Verifies the hex-native cave generator (docs/grid-topologies.md P1 polish): the map is a
    /// hex disc with a solid rim, every carved cell is reachable from the start over six-way
    /// adjacency, and generation is deterministic per seed.
    /// </summary>
    [TestFixture]
    public class HexCavesGeneratorTests
    {
        private const int Size = 40;
        private const int Seed = 1234;

        private static (Aetherium.Core.World World, GeneratorContext Context) Generate(int seed = Seed)
        {
            var context = new GeneratorContext(Size, Size, seed);
            var world = new HexCavesGenerator().Generate(context);
            world.Topology = HexTopology.Instance;
            return (world, context);
        }

        [Test]
        public void DeclaresHexOnlySupport()
        {
            Assert.That(new HexCavesGenerator().SupportedTopologies, Is.EquivalentTo(new[] { "hex" }));
        }

        [Test]
        public void MapIsAHexDiscWithASolidRim()
        {
            var (world, _) = Generate();
            var hex = HexTopology.Instance;
            var center = new GridCoord(Size / 2, Size / 2, 0);
            int radius = Size / 2 - 1;

            Assert.That(world.EntitiesByLocation, Is.Not.Empty);
            foreach (var location in world.EntitiesByLocation.Keys)
            {
                var cell = new GridCoord(location.X, location.Y, location.Z);
                int distance = hex.Distance(center, cell);
                Assert.That(distance, Is.LessThanOrEqualTo(radius), $"cell {location} lies outside the disc");
                if (distance == radius)
                    Assert.That(world.PassableTerrain(location), Is.False, $"rim cell {location} must be Wall");
            }
        }

        [Test]
        public void EveryFloorCellIsReachableFromTheStart()
        {
            var (world, context) = Generate();
            var hex = HexTopology.Instance;

            var floor = world.EntitiesByLocation.Keys
                .Where(world.PassableTerrain)
                .Select(l => new GridCoord(l.X, l.Y, l.Z))
                .ToHashSet();
            Assert.That(floor, Is.Not.Empty, "the caves must have carved something");

            var start = new GridCoord(context.StartLocation!.X, context.StartLocation.Y, context.StartLocation.Z);
            Assert.That(floor, Does.Contain(start), "the start location must be a floor cell");

            var reached = new HashSet<GridCoord> { start };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(start);
            while (queue.Count > 0)
                foreach (var n in hex.Neighbors(queue.Dequeue()))
                    if (floor.Contains(n) && reached.Add(n))
                        queue.Enqueue(n);

            Assert.That(reached.Count, Is.EqualTo(floor.Count), "no floor cell may be sealed off from the start");
        }

        [Test]
        public void SameSeedProducesTheSameCaves()
        {
            var (a, _) = Generate();
            var (b, _) = Generate();

            string Fingerprint(Aetherium.Core.World world) => string.Join(";",
                world.EntitiesByLocation.Keys
                    .OrderBy(l => l.X).ThenBy(l => l.Y)
                    .Select(l => $"{l.X},{l.Y}:{world.PassableTerrain(l)}"));

            Assert.That(Fingerprint(a), Is.EqualTo(Fingerprint(b)));
        }

        [Test]
        public void PlacesALightAtTheStart()
        {
            var (world, context) = Generate();
            bool lit = world.Entities.Values.Any(e =>
                e.Has<LightSource>() && e.Has<WorldLocation>() && e.Get<WorldLocation>().Equals(context.StartLocation));
            Assert.That(lit, Is.True, "caves are dark; the start chamber carries a light source");
        }
    }
}
