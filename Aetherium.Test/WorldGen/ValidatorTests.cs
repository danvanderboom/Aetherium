using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class ValidatorTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        // ──────────────────────────────────────────────────────────────────────
        // AdvancedDungeon: missing objective → error
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_AdvancedDungeon_MissingObjective_ReturnsError()
        {
            // Use an orchestrator whose layout pass never sets ObjectiveLocation.
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new NoObjectiveLayoutPass(),
                new DungeonValidationPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 1
            });

            // Either a pipeline error OR a validation error should be present.
            bool hasObjectiveError =
                result.Errors.Any(e => e.Contains("Objective") || e.Contains("objective")) ||
                (result.Validation?.Errors?.Any(e => e.Contains("Objective") || e.Contains("objective")) ?? false);

            Assert.That(hasObjectiveError, Is.True,
                $"Expected an objective-missing error. Pipeline errors: {string.Join("; ", result.Errors)}. " +
                $"Validation errors: {string.Join("; ", result.Validation?.Errors ?? Enumerable.Empty<string>())}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Non-AdvancedDungeon: missing objective → no error (silently OK)
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_OutdoorTemplate_MissingObjective_IsNotAnError()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new OutdoorLayoutPass(),
                new OutdoorValidationPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Outdoor,
                LayoutGenerator = "PerlinTerrain",
                Width = 60, Height = 60, Seed = 42
            });

            // Objective may or may not be present; missing it must not produce an error.
            bool objectiveError =
                result.Errors.Any(e => e.Contains("Objective") || e.Contains("objective") || e.Contains("location missing")) ||
                (result.Validation?.Errors?.Any(e => e.Contains("Objective") || e.Contains("objective")) ?? false);

            Assert.That(objectiveError, Is.False,
                $"Outdoor template should not fail on missing objective. Errors: {string.Join("; ", result.Errors)}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // AdvancedDungeon: full generation produces valid world with no validator errors
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_AdvancedDungeon_FullGeneration_PassesValidation(
            [Values(42, 1337, 9001)] int seed)
        {
            var orchestrator = new WorldGenerationOrchestrator(
                _registry, BuildFullDungeonPipeline());

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 60, Height = 60, Seed = seed
            });

            Assert.That(result.Success, Is.True,
                $"seed={seed} Pipeline errors: {string.Join("; ", result.Errors)}\n" +
                $"Validation errors: {string.Join("; ", result.Validation?.Errors ?? Enumerable.Empty<string>())}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Locked door + key reachability via the validator's BFS proof
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_LockedDoor_KeyReachabilityProofRecorded()
        {
            // Run the full dungeon pipeline: if gating exists the validator must record proofs.
            var orchestrator = new WorldGenerationOrchestrator(
                _registry, BuildFullDungeonPipeline());

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 60, Height = 60, Seed = 1337,
                EnableMetrics = true
            });

            Assert.That(result.Success, Is.True,
                $"Pipeline errors: {string.Join("; ", result.Errors)}");

            // If the validator ran and a locked door was found, a proof artifact should exist.
            if (result.Metrics.LockedDoors > 0 && result.Validation != null)
            {
                bool hasProof = result.Validation.ProofArtifacts.ContainsKey("start-to-objective");
                Assert.That(hasProof, Is.True,
                    "When a locked door is present the validator must record a start-to-objective proof path.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Locked doors without keys → validation error
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_LockedDoorsWithoutKeys_ProducesError()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new DungeonValidationPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 60, Height = 60, Seed = 1
            });

            // Only assert if the generator actually placed locked doors without keys
            // (which GenerationMetrics can tell us).
            if (result.Metrics.LockedDoors > 0 && result.Metrics.KeysPlaced == 0)
            {
                bool hasKeyError = result.Errors.Any(e => e.Contains("key") || e.Contains("Key")) ||
                                   (result.Validation?.Errors?.Any(e => e.Contains("key") || e.Contains("Key")) ?? false);
                Assert.That(hasKeyError, Is.True,
                    "Locked doors without keys should produce a validation error.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static IWorldGenerationPass[] BuildFullDungeonPipeline() => new IWorldGenerationPass[]
        {
            new DungeonLayoutPass(),
            new DungeonInteractionsPass(),
            new DungeonPopulationPass(),
            new DungeonValidationPass()
        };

        /// <summary>Minimal layout pass that creates a world but never assigns an objective.</summary>
        private sealed class NoObjectiveLayoutPass : IWorldGenerationPass
        {
            public string Name => "no-objective-layout";
            public GenerationPhase Phase => GenerationPhase.Layout;
            public bool SupportsTemplate(WorldGenerationTemplate t) => true;

            public void Execute(WorldGenerationContext context)
            {
                // Delegate to real layout so the world exists, then clear the objective.
                var registry = new MapGeneratorRegistry();
                registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
                var gen = registry.GetGenerator("AdvancedDungeon");
                if (gen != null)
                    context.World = gen.Generate(context.GeneratorContext);

                // Force-remove the objective to test the "missing objective" code path.
                context.GeneratorContext.ObjectiveLocation = WorldLocation.None;
            }
        }
    }
}
