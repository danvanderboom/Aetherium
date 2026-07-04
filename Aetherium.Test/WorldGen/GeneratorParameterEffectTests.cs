using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Verifies that generation parameters (curriculum/benchmark difficulty knobs) measurably affect
    /// the generated world, that absent parameters preserve default behavior, and that a difficulty
    /// profile is computed. Complements <see cref="DeterminismTests"/> (which covers seed → world).
    /// </summary>
    [TestFixture]
    public class GeneratorParameterEffectTests
    {
        private MapGeneratorRegistry _registry = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new MapGeneratorRegistry();
            _registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
        }

        private WorldGenerationResult Generate(
            int seed,
            Dictionary<string, string>? parameters = null,
            bool withPopulation = false,
            int width = 70,
            int height = 70)
        {
            IWorldGenerationPass[] passes = withPopulation
                ? new IWorldGenerationPass[] { new DungeonLayoutPass(), new DungeonPopulationPass() }
                : new IWorldGenerationPass[] { new DungeonLayoutPass() };

            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = width,
                Height = height,
                Seed = seed,
                RecordTimings = false
            };
            if (parameters != null)
            {
                foreach (var kv in parameters)
                    request.Parameters[kv.Key] = kv.Value;
            }

            return new WorldGenerationOrchestrator(_registry, passes).Generate(request);
        }

        private static string HashWorld(World world)
        {
            var sb = new StringBuilder();
            foreach (var loc in world.EntitiesByLocation.Keys
                .OrderBy(l => l.Z).ThenBy(l => l.Y).ThenBy(l => l.X))
            {
                sb.Append(loc.X).Append(',').Append(loc.Y).Append(',').Append(loc.Z)
                  .Append(':').Append(world.GetTerrainType(loc)?.Name ?? "null").Append(';');
            }
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        // ── Room count ────────────────────────────────────────────────────────

        [Test]
        public void MoreRoomsRequested_ProducesMoreRooms()
        {
            var few = Generate(42, new() { ["minRooms"] = "3", ["maxRooms"] = "3" });
            var many = Generate(42, new() { ["minRooms"] = "14", ["maxRooms"] = "14" });

            Assert.That(few.Success, Is.True, string.Join("; ", few.Errors));
            Assert.That(many.Success, Is.True, string.Join("; ", many.Errors));
            Assert.That(many.Metrics.Rooms, Is.GreaterThan(few.Metrics.Rooms),
                $"maxRooms=14 produced {many.Metrics.Rooms} rooms, not more than maxRooms=3's {few.Metrics.Rooms}");
        }

        // ── Traps ─────────────────────────────────────────────────────────────

        [Test]
        public void HigherTrapDensity_PlacesMoreTraps()
        {
            var baseline = Generate(42); // no trapDensity => exactly one trap
            var dense = Generate(42, new() { ["trapDensity"] = "1.0" });

            Assert.That(baseline.Success, Is.True, string.Join("; ", baseline.Errors));
            Assert.That(dense.Success, Is.True, string.Join("; ", dense.Errors));
            Assert.That(baseline.Metrics.TrapsPlaced, Is.EqualTo(1), "Default generation should place exactly one trap.");
            Assert.That(dense.Metrics.TrapsPlaced, Is.GreaterThan(1),
                $"trapDensity=1.0 placed {dense.Metrics.TrapsPlaced} traps, expected more than one.");
        }

        // ── Monsters (population pass) ─────────────────────────────────────────

        [Test]
        public void EnemyCount_ControlsMonsterCount()
        {
            var few = Generate(7, new() { ["enemyCount"] = "3" }, withPopulation: true);
            var many = Generate(7, new() { ["enemyCount"] = "25" }, withPopulation: true);

            Assert.That(few.Success, Is.True, string.Join("; ", few.Errors));
            Assert.That(many.Success, Is.True, string.Join("; ", many.Errors));

            int fewMonsters = few.World!.Entities.Values.OfType<Monster>().Count();
            int manyMonsters = many.World!.Entities.Values.OfType<Monster>().Count();

            Assert.That(fewMonsters, Is.EqualTo(3), "enemyCount=3 should place exactly 3 monsters.");
            Assert.That(manyMonsters, Is.GreaterThan(fewMonsters),
                $"enemyCount=25 placed {manyMonsters} monsters, not more than enemyCount=3's {fewMonsters}.");
        }

        // ── Treasure (population pass) ─────────────────────────────────────────

        [Test]
        public void ResourceAvailability_ControlsTreasureCount()
        {
            var baseline = Generate(7, withPopulation: true); // no param => 2 treasure items
            var abundant = Generate(7, new() { ["resourceAvailability"] = "3.0" }, withPopulation: true);

            static int TreasureCount(World w) =>
                w.Entities.Values.Count(e => e is HealthRestorativeItem || e is LanternItem);

            Assert.That(baseline.Success, Is.True, string.Join("; ", baseline.Errors));
            Assert.That(abundant.Success, Is.True, string.Join("; ", abundant.Errors));
            Assert.That(TreasureCount(baseline.World!), Is.EqualTo(2), "Default treasure should be the restorative + lantern pair.");
            Assert.That(TreasureCount(abundant.World!), Is.GreaterThan(2),
                "resourceAvailability=3.0 should place more than the default two treasure items.");
        }

        // ── Difficulty profile introspection ──────────────────────────────────

        [Test]
        public void DifficultyProfile_IsComputed_ForParameterizedRequest()
        {
            var result = Generate(42, new()
            {
                ["enemyCount"] = "12",
                ["trapDensity"] = "0.6",
                ["keyLockChainDepth"] = "3",
                ["combatDifficulty"] = "0.7",
                ["resourceAvailability"] = "0.3"
            });

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Metrics.DifficultyProfile, Is.Not.Null, "Difficulty profile should be computed.");
            Assert.That(result.Metrics.DifficultyProfile!.DifficultyScore, Is.GreaterThan(0),
                "A request with combat/puzzle parameters should have a non-zero difficulty score.");
            Assert.That(result.Metrics.PredictedAgentSuccessRate, Is.Not.Null,
                "Predicted success rate should be populated from the difficulty profile.");
        }

        [Test]
        public void DifficultyProfile_IsComputed_EvenWithoutParameters()
        {
            var result = Generate(42);
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Metrics.DifficultyProfile, Is.Not.Null,
                "Difficulty profile should be computed even for an unparameterized request.");
        }

        // ── Determinism under parameters ──────────────────────────────────────

        [Test]
        public void SameSeedAndParameters_ProduceIdenticalWorld()
        {
            var parameters = new Dictionary<string, string>
            {
                ["minRooms"] = "10",
                ["maxRooms"] = "12",
                ["trapDensity"] = "0.5"
            };
            var r1 = Generate(1337, new(parameters));
            var r2 = Generate(1337, new(parameters));

            Assert.That(r1.Success, Is.True, string.Join("; ", r1.Errors));
            Assert.That(r2.Success, Is.True, string.Join("; ", r2.Errors));
            Assert.That(HashWorld(r1.World!), Is.EqualTo(HashWorld(r2.World!)),
                "Same seed + same parameters must produce an identical world.");
        }

        // ── Key/lock chain depth (slice 2) ────────────────────────────────────

        private WorldGenerationResult GenerateFull(int seed, Dictionary<string, string>? parameters, int width = 80, int height = 80, int levels = 1)
        {
            var passes = new IWorldGenerationPass[]
            {
                new DungeonLayoutPass(),
                new DungeonInteractionsPass(),
                new DungeonPopulationPass(),
                new DungeonValidationPass()
            };
            var request = new WorldGenerationRequest
            {
                Template = WorldGenerationTemplate.Dungeon,
                LayoutGenerator = "AdvancedDungeon",
                Width = width, Height = height, Levels = levels, Seed = seed,
                RecordTimings = false
            };
            if (parameters != null)
                foreach (var kv in parameters) request.Parameters[kv.Key] = kv.Value;
            return new WorldGenerationOrchestrator(_registry, passes).Generate(request);
        }

        [Test]
        public void KeyLockChainDepth_PlacesMultipleValidGates()
        {
            var roomParams = new Dictionary<string, string> { ["minRooms"] = "12", ["maxRooms"] = "12" };
            var single = GenerateFull(42, new(roomParams)); // no depth => historical single gate
            var chain = GenerateFull(42, new(roomParams) { ["keyLockChainDepth"] = "4" }, levels: 2);

            Assert.That(single.Success, Is.True, string.Join("; ", single.Errors));
            Assert.That(single.Metrics.LockedDoors, Is.EqualTo(1), "Default generation gates exactly one door.");

            Assert.That(chain.Success, Is.True,
                $"Chained gates should still generate + validate.\nErrors: {string.Join("; ", chain.Errors)}\n" +
                $"Validation: {string.Join("; ", chain.Validation?.Errors ?? System.Linq.Enumerable.Empty<string>())}");
            Assert.That(chain.Metrics.LockedDoors, Is.GreaterThan(1), "keyLockChainDepth=4 should place more than one gate.");
            Assert.That(chain.Metrics.KeysPlaced, Is.EqualTo(chain.Metrics.LockedDoors),
                "Every gated door must have a matching key.");
            // The access-proof invariant (each key reachable from start without crossing a locked
            // door; doors collectively cut the objective) is enforced by the validator.
            Assert.That(chain.Validation, Is.Not.Null);
            Assert.That(chain.Validation!.Success, Is.True,
                $"Gating access proof failed for the chain: {string.Join("; ", chain.Validation.Errors)}");
        }

        // ── Secret room density (slice 2) ─────────────────────────────────────

        [Test]
        public void SecretRoomDensity_PlacesMoreSecrets()
        {
            var one = Generate(42); // absent => one secret
            var many = Generate(42, new() { ["secretRoomDensity"] = "1.0" });

            Assert.That(one.Success, Is.True, string.Join("; ", one.Errors));
            Assert.That(many.Success, Is.True, string.Join("; ", many.Errors));
            Assert.That(one.Metrics.SecretsPlaced, Is.EqualTo(1), "Default generation places one secret room.");
            Assert.That(many.Metrics.SecretsPlaced, Is.GreaterThan(1),
                $"secretRoomDensity=1.0 placed {many.Metrics.SecretsPlaced} secrets, expected more than one.");
        }

        // ── Outdoor population (slice 2) ──────────────────────────────────────

        [Test]
        public void OutdoorEnemyCount_ControlsMonsterCount()
        {
            WorldGenerationResult Outdoor(int seed, int enemies)
            {
                var passes = new IWorldGenerationPass[] { new OutdoorLayoutPass(), new OutdoorPopulationPass() };
                var request = new WorldGenerationRequest
                {
                    Template = WorldGenerationTemplate.Outdoor,
                    LayoutGenerator = "PerlinTerrain",
                    Width = 60, Height = 60, Seed = seed, RecordTimings = false
                };
                request.Parameters["enemyCount"] = enemies.ToString();
                return new WorldGenerationOrchestrator(_registry, passes).Generate(request);
            }

            var few = Outdoor(99, 1);
            var many = Outdoor(99, 8);
            Assert.That(few.Success, Is.True, string.Join("; ", few.Errors));
            Assert.That(many.Success, Is.True, string.Join("; ", many.Errors));

            int fewMonsters = few.World!.Entities.Values.OfType<Monster>().Count();
            int manyMonsters = many.World!.Entities.Values.OfType<Monster>().Count();
            Assert.That(manyMonsters, Is.GreaterThan(fewMonsters),
                $"enemyCount=8 placed {manyMonsters} monsters, not more than enemyCount=1's {fewMonsters}.");
        }
    }
}
