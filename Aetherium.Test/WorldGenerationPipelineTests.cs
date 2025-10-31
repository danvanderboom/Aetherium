using System;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test
{
    [TestFixture]
    public class WorldGenerationPipelineTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        [Test]
        public void DungeonPipeline_IsDeterministic_WithSeedAndVersion()
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 60,
                Height = 60,
                Levels = 2,
                Seed = 1337,
                GeneratorVersion = "2.0.0"
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry, BuildDungeonPasses());

            var first = orchestrator.Generate(request);
            var second = orchestrator.Generate(request);

            Assert.That(first.Success, Is.True, string.Join(";", first.Errors));
            Assert.That(second.Success, Is.True, string.Join(";", second.Errors));
            Assert.That(first.Validation?.Success, Is.True, string.Join(";", first.Validation?.Errors ?? Enumerable.Empty<string>()));
            Assert.That(second.Validation?.Success, Is.True, string.Join(";", second.Validation?.Errors ?? Enumerable.Empty<string>()));

            Assert.That(first.Metrics.BranchingFactor, Is.EqualTo(second.Metrics.BranchingFactor));
            Assert.That(first.Metrics.LoopRatio, Is.EqualTo(second.Metrics.LoopRatio));
            Assert.That(first.Metrics.Rooms, Is.EqualTo(second.Metrics.Rooms));
            Assert.That(first.Metrics.TrapsPlaced, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void DungeonPipeline_PlacesLockedDoorAndMatchingKey()
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedDungeon",
                Template = WorldGenerationTemplate.Dungeon,
                Width = 60,
                Height = 60,
                Levels = 2,
                Seed = 9001
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry, BuildDungeonPasses());
            var result = orchestrator.Generate(request);

            Assert.That(result.Success, Is.True, string.Join(";", result.Errors));
            Assert.That(result.Validation?.Success, Is.True, string.Join(";", result.Validation?.Errors ?? Enumerable.Empty<string>()));

            var door = result.World!.Entities.Values.FirstOrDefault(e => e.Has<OpensAndCloses>() && e.Get<OpensAndCloses>()?.IsLocked == true);
            Assert.That(door, Is.Not.Null, "Locked door not found");
            var keyShape = door!.Get<OpensAndCloses>()!.KeyShape;
            var key = result.World.Entities.Values.FirstOrDefault(e => e.Has<Key>() && e.Get<Key>()?.KeyId == keyShape);
            Assert.That(key, Is.Not.Null, "Matching key not found");
        }

        [Test]
        public void OutdoorPipeline_ProducesBiomeVarietyAndValidation()
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = "AdvancedOutdoor",
                Template = WorldGenerationTemplate.Outdoor,
                Width = 80,
                Height = 80,
                Levels = 1,
                Seed = 4242
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry, BuildOutdoorPasses());
            var result = orchestrator.Generate(request);

            Assert.That(result.Success, Is.True, string.Join(";", result.Errors));
            Assert.That(result.Validation?.Success, Is.True, string.Join(";", result.Validation?.Errors ?? Enumerable.Empty<string>()));

            Assert.That(result.Metrics.BiomeCoverage.Count, Is.GreaterThanOrEqualTo(3));
        }

        private static IWorldGenerationPass[] BuildDungeonPasses() => new IWorldGenerationPass[]
        {
            new DungeonLayoutPass(),
            new DungeonThemingPass(),
            new DungeonPopulationPass(),
            new DungeonInteractionsPass(),
            new DungeonValidationPass()
        };

        private static IWorldGenerationPass[] BuildOutdoorPasses() => new IWorldGenerationPass[]
        {
            new OutdoorLayoutPass(),
            new OutdoorThemingPass(),
            new OutdoorPopulationPass(),
            new OutdoorInteractionsPass(),
            new OutdoorValidationPass()
        };
    }
}



