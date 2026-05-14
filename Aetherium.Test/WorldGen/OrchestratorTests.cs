using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test.WorldGen
{
    [TestFixture]
    public class OrchestratorTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Request immutability — the orchestrator must not mutate the caller's request
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_DoesNotMutateLayoutGeneratorOnRequest()
        {
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40, Height = 40, Seed = 1,
                LayoutGenerator = string.Empty   // intentionally blank → orchestrator resolves internally
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            orchestrator.Generate(request);

            Assert.That(request.LayoutGenerator, Is.EqualTo(string.Empty),
                "Orchestrator must not write back the resolved layout name onto the caller's request.");
        }

        [Test]
        public void Generate_SameRequestUsedTwice_ProducesSameMetrics()
        {
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 777,
                EnableMetrics = true
            };

            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            var r1 = orchestrator.Generate(request);
            var r2 = orchestrator.Generate(request);

            Assert.That(r1.Metrics.Rooms, Is.EqualTo(r2.Metrics.Rooms));
            Assert.That(r1.Metrics.Corridors, Is.EqualTo(r2.Metrics.Corridors));
        }

        // ──────────────────────────────────────────────────────────────────────
        // World == null surfaced as error
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_NoPassProducesWorld_SurfacesDescriptiveError()
        {
            // A pass that does nothing — world stays null.
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new NoOpPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 30, Height = 30
            });

            Assert.That(result.World, Is.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
            Assert.That(result.Errors.Any(e => e.Contains("world")), Is.True,
                $"Expected a descriptive 'no world' error, got: {string.Join("; ", result.Errors)}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // AbortedByPass tracking
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_PassThrowsException_SetsAbortedByPass()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new ThrowingPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 30, Height = 30
            });

            Assert.That(result.AbortedByPass, Is.EqualTo("throwing-pass"));
            Assert.That(result.Errors.Any(e => e.Contains("throwing-pass")), Is.True);
        }

        [Test]
        public void Generate_PipelineCompletes_AbortedByPassIsNull()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 1
            });

            Assert.That(result.AbortedByPass, Is.Null);
        }

        // ──────────────────────────────────────────────────────────────────────
        // EffectiveSeed propagation
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_WithExplicitSeed_EffectiveSeedMatchesSeed()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 42
            });

            Assert.That(result.EffectiveSeed, Is.EqualTo(42));
            Assert.That(result.Metrics.EffectiveSeed, Is.EqualTo(42));
        }

        [Test]
        public void Generate_WithoutSeed_EffectiveSeedIsNonZero()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            // Run twice without seed — effective seeds should be different (with overwhelming probability).
            var r1 = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40, Height = 40
            });
            var r2 = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 40, Height = 40
            });

            // Can't guarantee they're different, but they should not both be 0.
            Assert.That(r1.EffectiveSeed != 0 || r2.EffectiveSeed != 0, Is.True);
        }

        // ──────────────────────────────────────────────────────────────────────
        // RecordTimings flag
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_RecordTimingsFalse_NoPhaseDurationsRecorded()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 1,
                EnableMetrics = true,
                RecordTimings = false
            });

            Assert.That(result.Metrics.PhaseDurationsMs, Is.Empty,
                "No durations should be recorded when RecordTimings = false.");
        }

        [Test]
        public void Generate_RecordTimingsTrue_PhaseDurationsRecorded()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = 50, Height = 50, Seed = 1,
                EnableMetrics = true,
                RecordTimings = true
            });

            Assert.That(result.Metrics.PhaseDurationsMs.Count, Is.GreaterThan(0),
                "Phase durations should be recorded when RecordTimings = true.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Cancellation
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Generate_PreCancelledToken_SetsAbortedByPass()
        {
            var orchestrator = new WorldGenerationOrchestrator(_registry, new IWorldGenerationPass[]
            {
                new DungeonLayoutPass()
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = orchestrator.Generate(new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                Width = 30, Height = 30
            }, cts.Token);

            Assert.That(result.AbortedByPass, Is.Not.Null);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Stub passes
        // ──────────────────────────────────────────────────────────────────────

        private sealed class NoOpPass : IWorldGenerationPass
        {
            public string Name => "no-op";
            public GenerationPhase Phase => GenerationPhase.Layout;
            public bool SupportsTemplate(WorldGenerationTemplate t) => true;
            public void Execute(WorldGenerationContext context) { /* deliberately produces no world */ }
        }

        private sealed class ThrowingPass : IWorldGenerationPass
        {
            public string Name => "throwing-pass";
            public GenerationPhase Phase => GenerationPhase.Layout;
            public bool SupportsTemplate(WorldGenerationTemplate t) => true;
            public void Execute(WorldGenerationContext context) =>
                throw new InvalidOperationException("Deliberate test failure.");
        }
    }
}
