using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Generation invariant guard (from a live-smoke investigation into off-map H3 spawns): the shipped
    /// aphelion-h3 world — generated both directly and through the FULL Outdoor orchestrator pipeline,
    /// with the complete parameter set (slab + settlements + transit + satellites) — must yield walkable
    /// ground so the join path's World.SelectRandomPassableLocation() finds a spawn. (The live off-map
    /// spawn turned out to be snapshot rehydration losing the topology, covered by
    /// H3SnapshotRehydrationTests; generation itself was always sound, which these tests pin down.)
    /// Runs at a lower resolution for speed; passability is resolution-independent.
    /// </summary>
    [TestFixture]
    public class H3SpawnSelectionProbe
    {
        private static Aetherium.Core.World Generate(int resolution)
        {
            var context = new GeneratorContext(256, 256, 20260718)
            {
                GeneratorParams = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = resolution.ToString(),
                    // z-altitude / slab (matches Data/Games/aphelion-h3/game.yaml)
                    ["minBand"] = "-3", ["maxBand"] = "64", ["slabDepthBelow"] = "2", ["slabDepthAbove"] = "3",
                    // satellites
                    ["satelliteCount"] = "18", ["satelliteBaseBand"] = "24", ["satelliteBandGap"] = "2",
                    ["satelliteMinRadius"] = "25", ["satelliteMaxRadius"] = "70",
                    ["satelliteMinPeriod"] = "1", ["satelliteMaxPeriod"] = "5",
                    // rivers / settlements / roads
                    ["riverCount"] = "45", ["riverMouthWidth"] = "3", ["riverWidenSteps"] = "10",
                    ["capitalCount"] = "6", ["cityCount"] = "24", ["townCount"] = "70", ["villageCount"] = "220",
                    ["capitalSpacingCells"] = "80", ["citySpacingCells"] = "40",
                    ["townSpacingCells"] = "18", ["villageSpacingCells"] = "9",
                    ["roadNeighbors"] = "2", ["highwayWidth"] = "2", ["roadWidth"] = "1",
                    // transit
                    ["transit"] = "1", ["railCapacity"] = "6", ["subwayCapacity"] = "8", ["subwayBand"] = "-2",
                }
            };
            return new H3TerrainGenerator().Generate(context);
        }

        [Test]
        public void RandomPassableSpawnFindsGround()
        {
            var world = Generate(resolution: 3);

            int passable = world.EntitiesByLocation.Keys.Count(world.PassableTerrain);
            TestContext.WriteLine($"cells={world.EntitiesByLocation.Keys.Count}  passable={passable}");

            Assert.That(passable, Is.GreaterThan(0), "the planet must have walkable ground to spawn on");

            var pick = world.SelectRandomPassableLocation();
            Assert.That(pick, Is.Not.Null,
                "SelectRandomPassableLocation must return a walkable cell (the join path depends on it)");
            Assert.That(world.PassableTerrain(pick!), Is.True);
        }

        // The runtime path: GameMapGrain builds the world through the FULL Outdoor orchestrator
        // (WorldGenerationPassCatalog), not a bare generator call. This reproduces that path to see
        // whether a post-layout pass corrupts passability on the sphere (which would break joins).
        private static Aetherium.Core.World GenerateThroughPipeline(int resolution)
        {
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "h3-terrain",
                Width = 256,
                Height = 256,
                Levels = 1,
                Seed = 20260718,
                Template = WorldGenerationTemplate.Outdoor,
                Topology = "h3",
                Parameters = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = resolution.ToString(),
                    ["minBand"] = "-3", ["maxBand"] = "64", ["slabDepthBelow"] = "2", ["slabDepthAbove"] = "3",
                    ["satelliteCount"] = "18", ["satelliteBaseBand"] = "24", ["satelliteBandGap"] = "2",
                    ["capitalCount"] = "6", ["cityCount"] = "24", ["townCount"] = "70", ["villageCount"] = "220",
                    ["transit"] = "1", ["subwayBand"] = "-2",
                },
            };
            var orchestrator = new WorldGenerationOrchestrator(registry, WorldGenerationPassCatalog.BuildPasses(request.Template));
            var result = orchestrator.Generate(request);
            Assert.That(result.World, Is.Not.Null, "pipeline produced no world: " + string.Join("; ", result.Errors));
            return result.World!;
        }

        [Test]
        public void FullPipelineSpawnFindsGround()
        {
            var world = GenerateThroughPipeline(resolution: 3);

            int passable = world.EntitiesByLocation.Keys.Count(world.PassableTerrain);
            Assert.That(passable, Is.GreaterThan(0),
                "after the full Outdoor pipeline the planet must still have walkable ground");
            Assert.That(world.SelectRandomPassableLocation(), Is.Not.Null,
                "the runtime join path (SelectRandomPassableLocation) must find ground after the full pipeline");
        }
    }
}
