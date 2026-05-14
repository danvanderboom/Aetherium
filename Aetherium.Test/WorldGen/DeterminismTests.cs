using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Verifies that seed → world is reproducible within a single runtime.
    /// These tests are explicitly single-platform (Windows x64 / .NET 10) and are not
    /// asserted cross-platform (runtime differences in floating point or RNG implementation
    /// may differ).
    /// </summary>
    [TestFixture]
    public class DeterminismTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a stable string hash of a world's tile layout: for every location
        /// in Z/Y/X order, appends the terrain name. Same world → same hash.
        /// </summary>
        private static string HashWorld(World world)
        {
            var sb = new StringBuilder();
            foreach (var loc in world.EntitiesByLocation.Keys
                .OrderBy(l => l.Z).ThenBy(l => l.Y).ThenBy(l => l.X))
            {
                sb.Append(loc.X).Append(',').Append(loc.Y).Append(',').Append(loc.Z)
                  .Append(':').Append(world.GetTerrainType(loc)?.Name ?? "null")
                  .Append(';');
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(bytes);
        }

        private WorldGenerationResult Generate(int seed, string template = "AdvancedDungeon")
        {
            var isOutdoor = template.Contains("Outdoor") || template.Contains("Perlin");
            var wgTemplate = isOutdoor
                ? WorldGenerationTemplate.Outdoor
                : WorldGenerationTemplate.Dungeon;

            IWorldGenerationPass[] passes = isOutdoor
                ? new IWorldGenerationPass[] { new OutdoorLayoutPass() }
                : new IWorldGenerationPass[] { new DungeonLayoutPass() };

            var request = new WorldGenerationRequest
            {
                Template = wgTemplate,
                LayoutGenerator = template,
                Width = 50, Height = 50, Seed = seed,
                // Disable timing so wall-clock doesn't leak into metrics that callers might hash.
                RecordTimings = false
            };

            return new WorldGenerationOrchestrator(_registry, passes).Generate(request);
        }

        // ──────────────────────────────────────────────────────────────────────
        // In-process determinism: same seed → identical world hash
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void AdvancedDungeon_SameSeed_SameWorldHash([Values(1, 42, 1337)] int seed)
        {
            var r1 = Generate(seed);
            var r2 = Generate(seed);

            Assert.That(r1.Success, Is.True,
                $"seed={seed}: {string.Join("; ", r1.Errors)}");
            Assert.That(r2.Success, Is.True,
                $"seed={seed} (2nd run): {string.Join("; ", r2.Errors)}");

            var hash1 = HashWorld(r1.World!);
            var hash2 = HashWorld(r2.World!);

            Assert.That(hash1, Is.EqualTo(hash2),
                $"seed={seed}: World hash differs between runs — RNG stream is not reproducible.");
        }

        [Test]
        public void AdvancedDungeon_DifferentSeeds_DifferentWorldHash()
        {
            var r42 = Generate(42);
            var r43 = Generate(43);

            Assert.That(r42.Success, Is.True);
            Assert.That(r43.Success, Is.True);

            var hash42 = HashWorld(r42.World!);
            var hash43 = HashWorld(r43.World!);

            Assert.That(hash42, Is.Not.EqualTo(hash43),
                "Different seeds should produce different worlds.");
        }

        [Test]
        public void OutdoorTerrain_SameSeed_SameWorldHash([Values(1, 99)] int seed)
        {
            var r1 = Generate(seed, "PerlinTerrain");
            var r2 = Generate(seed, "PerlinTerrain");

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);

            Assert.That(HashWorld(r1.World!), Is.EqualTo(HashWorld(r2.World!)),
                $"PerlinTerrain seed={seed}: world hash not stable.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Scoped RNG derivation: generator version changes seed stream
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void DifferentGeneratorVersion_ProducesDifferentWorld()
        {
            static WorldGenerationResult Run(string version)
            {
                var registry = new MapGeneratorRegistry();
                registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

                return new WorldGenerationOrchestrator(registry, new IWorldGenerationPass[]
                {
                    new DungeonLayoutPass()
                }).Generate(new WorldGenerationRequest
                {
                    Template = WorldGenerationTemplate.Dungeon,
                    LayoutGenerator = "AdvancedDungeon",
                    Width = 50, Height = 50, Seed = 1,
                    GeneratorVersion = version,
                    RecordTimings = false
                });
            }

            var v1 = Run("1.0.0");
            var v2 = Run("2.0.0");

            Assert.That(v1.Success, Is.True);
            Assert.That(v2.Success, Is.True);

            Assert.That(HashWorld(v1.World!), Is.Not.EqualTo(HashWorld(v2.World!)),
                "Different GeneratorVersion values should produce different worlds from the same seed.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // EffectiveSeed round-trip
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void EffectiveSeed_CanBeUsedToReplayWorld()
        {
            // First run — no explicit seed (random).
            var registry = new MapGeneratorRegistry();
            registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
            var passes = new IWorldGenerationPass[] { new DungeonLayoutPass() };

            var first = new WorldGenerationOrchestrator(registry, passes).Generate(
                new WorldGenerationRequest
                {
                    Template = WorldGenerationTemplate.Dungeon,
                    LayoutGenerator = "AdvancedDungeon",
                    Width = 50, Height = 50,
                    RecordTimings = false
                });

            Assert.That(first.Success, Is.True);

            int savedSeed = first.EffectiveSeed;

            // Replay using the saved seed.
            var replay = new WorldGenerationOrchestrator(registry, passes).Generate(
                new WorldGenerationRequest
                {
                    Template = WorldGenerationTemplate.Dungeon,
                    LayoutGenerator = "AdvancedDungeon",
                    Width = 50, Height = 50, Seed = savedSeed,
                    RecordTimings = false
                });

            Assert.That(replay.Success, Is.True);
            Assert.That(HashWorld(replay.World!), Is.EqualTo(HashWorld(first.World!)),
                "Replaying with the saved EffectiveSeed should produce the identical world.");
        }
    }
}
