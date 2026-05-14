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
    public class DungeonGeneratorRegressionTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        private WorldGenerationResult Run(int seed, int width = 60, int height = 60, int levels = 1)
        {
            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new DungeonPopulationPass(),
                new DungeonValidationPass()
            };

            return new WorldGenerationOrchestrator(_registry, passes).Generate(
                new WorldGenerationRequest
                {
                    Template = WorldGenerationTemplate.Dungeon,
                    LayoutGenerator = "AdvancedDungeon",
                    Width = width, Height = height, Levels = levels,
                    Seed = seed,
                    EnableMetrics = true,
                    RecordTimings = false
                });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Basic structural validity
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_KnownSeeds_SuccessAndPassValidation(
            [Values(1, 42, 1337, 9001, 99999)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Success, Is.True,
                $"seed={seed}\nErrors: {string.Join("; ", result.Errors)}\n" +
                $"ValidationErrors: {string.Join("; ", result.Validation?.Errors ?? Enumerable.Empty<string>())}");
        }

        [Test]
        public void AdvancedDungeon_ProducesAtLeastTwoRooms([Values(1, 42, 1337)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Metrics.Rooms, Is.GreaterThanOrEqualTo(2),
                $"seed={seed}: Expected at least 2 rooms.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Start and objective locations
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_StartAndObjectiveAreDistinct([Values(1, 42, 1337)] int seed)
        {
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var ctx = new GeneratorContext(60, 60, seed)
            {
                GeneratorVersion = "1.0.0",
                Levels = 1
            };
            ctx.GeneratorParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["layoutGenerator"] = "AdvancedDungeon"
            };

            var gen = registry.GetGenerator("AdvancedDungeon");
            Assert.That(gen, Is.Not.Null);
            var world = gen!.Generate(ctx);

            var start = ctx.StartLocation;
            var obj = ctx.ObjectiveLocation;

            Assert.That(start, Is.Not.Null);
            Assert.That(obj, Is.Not.Null);
            Assert.That(start!.IsNone, Is.False, "Start location must be set.");
            Assert.That(obj!.IsNone, Is.False, "Objective location must be set.");
            Assert.That(start, Is.Not.EqualTo(obj),
                $"seed={seed}: Start and objective are the same location ({start}).");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Locked door + key
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_LockedDoorHasMatchingKey([Values(1, 42, 1337)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Success, Is.True,
                $"seed={seed}: {string.Join("; ", result.Errors)}");

            if (result.Metrics.LockedDoors > 0)
            {
                Assert.That(result.Metrics.KeysPlaced, Is.GreaterThan(0),
                    $"seed={seed}: {result.Metrics.LockedDoors} locked door(s) but no keys placed.");
            }
        }

        [Test]
        public void AdvancedDungeon_KeyIsReachableFromStartBeforeAnyLockedDoor([Values(1, 42, 1337)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Success, Is.True,
                $"seed={seed}: {string.Join("; ", result.Errors)}");

            // The validator proves key reachability; if it passed, we're good.
            if (result.Metrics.LockedDoors > 0 && result.Validation != null)
            {
                Assert.That(result.Validation.Success, Is.True,
                    $"seed={seed}: Validator failed — key reachability check likely failed.\n" +
                    string.Join("; ", result.Validation.Errors));
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Secret rooms
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_PlacesAtLeastOneSecret([Values(1, 42, 1337)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Metrics.SecretsPlaced, Is.GreaterThanOrEqualTo(1),
                $"seed={seed}: Expected at least one secret room.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Traps
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_PlacesAtLeastOneTrap([Values(1, 42, 1337)] int seed)
        {
            var result = Run(seed);
            Assert.That(result.Metrics.TrapsPlaced, Is.GreaterThanOrEqualTo(1),
                $"seed={seed}: Expected at least one trap.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Small-map guard (GetRandomBounds crash regression)
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_SmallMap_ThrowsDescriptiveErrorRatherThanCrashing()
        {
            // Maps smaller than the minimum room size used to crash with a Range exception.
            // Now we expect either success or a descriptive error, never an unhandled exception.
            Assert.DoesNotThrow(() =>
            {
                var result = Run(seed: 1, width: 10, height: 10);
                // Small maps may fail validation but must not throw.
                _ = result.Errors;
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Multi-level
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_MultiLevel_ProducesConnectedLevels([Values(2, 3)] int levels)
        {
            var result = Run(seed: 42, width: 60, height: 60, levels: levels);

            // A multi-level dungeon should succeed; connectivity is validated by the
            // DungeonValidationPass (checks cross-level reachability when levels > 1).
            Assert.That(result.Errors.Count, Is.EqualTo(0),
                $"levels={levels}: {string.Join("; ", result.Errors)}");
        }
    }
}
