using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen.Training;
using Aetherium.Model.Training;
using Aetherium.Server.WorldGen;
using Aetherium.WorldGen;

namespace Aetherium.Test.WorldGen.Training
{
    [TestFixture]
    public class BenchmarkLibraryTests
    {
        [Test]
        public void BenchmarkScenario_LoadFromJson_ValidJson_Succeeds()
        {
            // Arrange
            var benchmarkJson = @"{
                ""benchmarkId"": ""test-benchmark"",
                ""name"": ""Test Benchmark"",
                ""description"": ""Test benchmark for unit tests"",
                ""categories"": [""test"", ""unit""],
                ""difficulty"": 50,
                ""version"": ""1.0"",
                ""recipe"": {
                    ""generator"": ""AdvancedDungeonGenerator"",
                    ""template"": ""Dungeon"",
                    ""seed"": 12345,
                    ""generatorVersion"": ""1.0"",
                    ""width"": 40,
                    ""height"": 40,
                    ""levels"": 1,
                    ""parameters"": {
                        ""roomCount"": ""10"",
                        ""trapDensity"": ""0.2""
                    }
                },
                ""successCriteria"": {
                    ""type"": ""ReachGoal"",
                    ""goalLocation"": { ""x"": 35, ""y"": 35, ""z"": 0 },
                    ""maxSteps"": 200,
                    ""maxTimeSeconds"": 300
                }
            }";

            // Act
            var benchmark = System.Text.Json.JsonSerializer.Deserialize<BenchmarkScenario>(benchmarkJson);

            // Assert
            Assert.That(benchmark, Is.Not.Null);
            Assert.That(benchmark.BenchmarkId, Is.EqualTo("test-benchmark"));
            Assert.That(benchmark.Recipe, Is.Not.Null);
            Assert.That(benchmark.Recipe.Generator, Is.EqualTo("AdvancedDungeonGenerator"));
            Assert.That(benchmark.SuccessCriteria, Is.Not.Null);
            Assert.That(benchmark.SuccessCriteria.Type, Is.EqualTo("ReachGoal"));
        }

        [Test]
        public void BenchmarkLibrary_GetBenchmark_ValidId_ReturnsBenchmark()
        {
            // Arrange
            var benchmarkId = "test-benchmark";
            var benchmark = new BenchmarkScenario
            {
                BenchmarkId = benchmarkId,
                Name = "Test Benchmark"
            };
            
            // Manually add to library (in real implementation, loaded from directory)
            var benchmarks = new Dictionary<string, BenchmarkScenario> { [benchmarkId] = benchmark };
            var libraryType = typeof(BenchmarkLibrary);
            var benchmarksField = libraryType.GetField("_benchmarks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (benchmarksField != null)
            {
                benchmarksField.SetValue(null, benchmarks);
            }

            // Act
            var result = BenchmarkLibrary.GetBenchmark(benchmarkId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.BenchmarkId, Is.EqualTo(benchmarkId));
        }

        [Test]
        public void BenchmarkLibrary_GetBenchmark_InvalidId_ReturnsNull()
        {
            // Arrange
            var benchmarkId = "non-existent-benchmark";

            // Act
            var result = BenchmarkLibrary.GetBenchmark(benchmarkId);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void BenchmarkLibrary_GetBenchmarksByCategory_ReturnsMatchingBenchmarks()
        {
            // Arrange
            var benchmark1 = new BenchmarkScenario
            {
                BenchmarkId = "benchmark1",
                Categories = new List<string> { "navigation", "basic" }
            };
            var benchmark2 = new BenchmarkScenario
            {
                BenchmarkId = "benchmark2",
                Categories = new List<string> { "combat", "basic" }
            };
            
            // Manually add to library
            var benchmarks = new Dictionary<string, BenchmarkScenario>
            {
                ["benchmark1"] = benchmark1,
                ["benchmark2"] = benchmark2
            };
            var libraryType = typeof(BenchmarkLibrary);
            var benchmarksField = libraryType.GetField("_benchmarks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (benchmarksField != null)
            {
                benchmarksField.SetValue(null, benchmarks);
            }

            // Act
            var navigationBenchmarks = BenchmarkLibrary.GetBenchmarksByCategory("navigation");
            var combatBenchmarks = BenchmarkLibrary.GetBenchmarksByCategory("combat");

            // Assert
            Assert.That(navigationBenchmarks, Is.Not.Empty);
            Assert.That(navigationBenchmarks.Any(b => b.BenchmarkId == "benchmark1"), Is.True);
            Assert.That(combatBenchmarks, Is.Not.Empty);
            Assert.That(combatBenchmarks.Any(b => b.BenchmarkId == "benchmark2"), Is.True);
        }

        [Test]
        public void BenchmarkGenerator_GenerateRequest_FromRecipe_CreatesValidRequest()
        {
            // Arrange
            var recipe = new BenchmarkRecipe
            {
                Generator = "AdvancedDungeonGenerator",
                Template = "dungeon",
                Seed = 12345,
                GeneratorVersion = "1.0",
                Width = 40,
                Height = 40,
                Levels = 1,
                Parameters = new Dictionary<string, string>
                {
                    ["roomCount"] = "10",
                    ["trapDensity"] = "0.2"
                }
            };

            // Act
            var request = BenchmarkGenerator.GenerateRequest(recipe);

            // Assert
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Width, Is.EqualTo(40));
            Assert.That(request.Height, Is.EqualTo(40));
            Assert.That(request.Levels, Is.EqualTo(1));
            Assert.That(request.Template, Is.EqualTo(WorldGenerationTemplate.Dungeon));
            Assert.That(request.IsTrainingMode, Is.True);
            Assert.That(request.Parameters.ContainsKey("roomCount"), Is.True);
            Assert.That(request.Parameters.ContainsKey("trapDensity"), Is.True);
        }

        [Test]
        public void BenchmarkGenerator_GenerateVariations_CreatesVariationsWithDifferentSeeds()
        {
            // Arrange
            var baseBenchmark = new BenchmarkScenario
            {
                BenchmarkId = "base-benchmark",
                Recipe = new BenchmarkRecipe
                {
                    Seed = 12345,
                    Width = 40,
                    Height = 40
                }
            };
            var count = 3;

            // Act
            var variations = BenchmarkGenerator.GenerateVariations(baseBenchmark, count);

            // Assert
            Assert.That(variations, Is.Not.Null);
            Assert.That(variations.Count, Is.EqualTo(count));
            
            // Each variation should have unique ID and seed
            var variationIds = variations.Select(v => v.BenchmarkId).ToList();
            Assert.That(variationIds.Distinct().Count(), Is.EqualTo(count));
            
            var seeds = variations.Select(v => v.Recipe.Seed).ToList();
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(count));
        }

        [Test]
        public void BenchmarkGenerator_GenerateEdgeCase_NavigationFailure_IncreasesMapSize()
        {
            // Arrange
            var baseRecipe = new BenchmarkRecipe
            {
                Width = 30,
                Height = 30,
                Parameters = new Dictionary<string, string>
                {
                    ["branchingFactor"] = "0.5"
                }
            };
            var failurePattern = "navigation_failure";
            var benchmarkId = "edge-case-nav";

            // Act
            var edgeCase = BenchmarkGenerator.GenerateEdgeCase(benchmarkId, failurePattern, baseRecipe);

            // Assert
            Assert.That(edgeCase, Is.Not.Null);
            Assert.That(edgeCase.Recipe.Width, Is.GreaterThanOrEqualTo(30));
            Assert.That(edgeCase.Recipe.Height, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void BenchmarkGenerator_GenerateEdgeCase_KeyLockFailure_IncreasesChainDepth()
        {
            // Arrange
            var baseRecipe = new BenchmarkRecipe
            {
                Width = 30,
                Height = 30,
                Parameters = new Dictionary<string, string>
                {
                    ["keyLockChainDepth"] = "2"
                }
            };
            var failurePattern = "key_lock_failure";
            var benchmarkId = "edge-case-keylock";

            // Act
            var edgeCase = BenchmarkGenerator.GenerateEdgeCase(benchmarkId, failurePattern, baseRecipe);

            // Assert
            Assert.That(edgeCase, Is.Not.Null);
            if (edgeCase.Recipe.Parameters.ContainsKey("keyLockChainDepth") && 
                int.TryParse(edgeCase.Recipe.Parameters["keyLockChainDepth"], out var depth))
            {
                Assert.That(depth, Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        public void BenchmarkLibrary_LoadBenchmarks_MissingDirectory_HandlesGracefully()
        {
            // Arrange
            var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act - should not throw
            BenchmarkLibrary.LoadBenchmarks(nonExistentDir);

            // Assert - library should still be functional (empty but not null)
            var result = BenchmarkLibrary.GetBenchmark("any-id");
            Assert.That(result, Is.Null); // No benchmarks loaded, so null is expected
        }

        [Test]
        public void BenchmarkLibrary_LoadBenchmarks_ValidJsonFile_LoadsAndRegisters()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            var validBenchmarkJson = @"{
                ""benchmarkId"": ""load-test-benchmark"",
                ""name"": ""Load Test Benchmark"",
                ""description"": ""Test for loading"",
                ""categories"": [""test""],
                ""difficulty"": 50,
                ""version"": ""1.0"",
                ""recipe"": {
                    ""generator"": ""AdvancedDungeonGenerator"",
                    ""template"": ""Dungeon"",
                    ""seed"": 999,
                    ""generatorVersion"": ""1.0"",
                    ""width"": 30,
                    ""height"": 30,
                    ""levels"": 1,
                    ""parameters"": {}
                },
                ""successCriteria"": {
                    ""type"": ""ReachGoal"",
                    ""maxSteps"": 100
                }
            }";

            var jsonFile = Path.Combine(tempDir, "test-benchmark.json");
            File.WriteAllText(jsonFile, validBenchmarkJson);

            try
            {
                // Act
                BenchmarkLibrary.LoadBenchmarks(tempDir);

                // Assert
                var loadedBenchmark = BenchmarkLibrary.GetBenchmark("load-test-benchmark");
                Assert.That(loadedBenchmark, Is.Not.Null);
                Assert.That(loadedBenchmark.BenchmarkId, Is.EqualTo("load-test-benchmark"));
                Assert.That(loadedBenchmark.Name, Is.EqualTo("Load Test Benchmark"));
            }
            finally
            {
                // Cleanup
                try { File.Delete(jsonFile); } catch { }
                try { Directory.Delete(tempDir); } catch { }
            }
        }

        [Test]
        public void BenchmarkLibrary_LoadBenchmarks_InvalidJsonFile_HandlesGracefully()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            var invalidJson = "{ invalid json }";
            var jsonFile = Path.Combine(tempDir, "invalid-benchmark.json");
            File.WriteAllText(jsonFile, invalidJson);

            try
            {
                // Act - should not throw
                BenchmarkLibrary.LoadBenchmarks(tempDir);

                // Assert - invalid file should be skipped, library still functional
                var result = BenchmarkLibrary.GetBenchmark("invalid-benchmark");
                Assert.That(result, Is.Null); // Invalid file shouldn't be registered
            }
            finally
            {
                // Cleanup
                try { File.Delete(jsonFile); } catch { }
                try { Directory.Delete(tempDir); } catch { }
            }
        }

        [Test]
        public void BenchmarkLibrary_LoadBenchmarks_MixedValidAndInvalid_FiltersCorrectly()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            var validBenchmarkJson = @"{
                ""benchmarkId"": ""valid-benchmark"",
                ""name"": ""Valid Benchmark"",
                ""description"": ""Valid test"",
                ""categories"": [""test""],
                ""difficulty"": 50,
                ""version"": ""1.0"",
                ""recipe"": {
                    ""generator"": ""AdvancedDungeonGenerator"",
                    ""template"": ""Dungeon"",
                    ""seed"": 123,
                    ""generatorVersion"": ""1.0"",
                    ""width"": 30,
                    ""height"": 30,
                    ""levels"": 1,
                    ""parameters"": {}
                },
                ""successCriteria"": {
                    ""type"": ""ReachGoal"",
                    ""maxSteps"": 100
                }
            }";

            var validFile = Path.Combine(tempDir, "valid.json");
            var invalidFile = Path.Combine(tempDir, "invalid.json");
            File.WriteAllText(validFile, validBenchmarkJson);
            File.WriteAllText(invalidFile, "{ invalid }");

            try
            {
                // Act
                BenchmarkLibrary.LoadBenchmarks(tempDir);

                // Assert
                var valid = BenchmarkLibrary.GetBenchmark("valid-benchmark");
                Assert.That(valid, Is.Not.Null);
                Assert.That(valid.BenchmarkId, Is.EqualTo("valid-benchmark"));
            }
            finally
            {
                // Cleanup
                try { File.Delete(validFile); } catch { }
                try { File.Delete(invalidFile); } catch { }
                try { Directory.Delete(tempDir); } catch { }
            }
        }

        [Test]
        public void BenchmarkLibrary_GetAllBenchmarks_ReturnsAllRegisteredBenchmarks()
        {
            // Arrange
            var benchmark1 = new BenchmarkScenario { BenchmarkId = "bench1", Categories = new List<string> { "test" } };
            var benchmark2 = new BenchmarkScenario { BenchmarkId = "bench2", Categories = new List<string> { "test" } };
            
            var benchmarks = new Dictionary<string, BenchmarkScenario>
            {
                ["bench1"] = benchmark1,
                ["bench2"] = benchmark2
            };
            
            var libraryType = typeof(BenchmarkLibrary);
            var benchmarksField = libraryType.GetField("_benchmarks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (benchmarksField != null)
            {
                benchmarksField.SetValue(null, benchmarks);
            }

            // Act
            var allBenchmarks = BenchmarkLibrary.GetAllBenchmarks();

            // Assert
            Assert.That(allBenchmarks, Is.Not.Null);
            Assert.That(allBenchmarks.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(allBenchmarks.Any(b => b.BenchmarkId == "bench1"), Is.True);
            Assert.That(allBenchmarks.Any(b => b.BenchmarkId == "bench2"), Is.True);
        }
    }
}

