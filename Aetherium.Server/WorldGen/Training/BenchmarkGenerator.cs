using System;
using System.Collections.Generic;
using Aetherium.WorldGen;

using Aetherium.Model.Training;
namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Generates benchmark scenarios on-demand from recipes.
    /// </summary>
    public static class BenchmarkGenerator
    {
        /// <summary>
        /// Generates a world generation request from a benchmark recipe.
        /// </summary>
        public static WorldGenerationRequest GenerateRequest(BenchmarkRecipe recipe)
        {
            var request = new WorldGenerationRequest
            {
                LayoutGenerator = recipe.Generator,
                Template = Enum.TryParse<WorldGenerationTemplate>(recipe.Template, true, out var template)
                    ? template
                    : WorldGenerationTemplate.Dungeon,
                Width = recipe.Width,
                Height = recipe.Height,
                Levels = recipe.Levels,
                Seed = recipe.Seed,
                GeneratorVersion = recipe.GeneratorVersion,
                Parameters = new Dictionary<string, string>(recipe.Parameters, StringComparer.OrdinalIgnoreCase),
                EnableMetrics = true,
                IsTrainingMode = true
            };

            return request;
        }

        /// <summary>
        /// Generates a benchmark scenario on-demand from a recipe.
        /// </summary>
        public static BenchmarkScenario GenerateBenchmark(
            string benchmarkId,
            string name,
            string description,
            BenchmarkRecipe recipe,
            SuccessCriteria successCriteria,
            int difficulty = 5)
        {
            return new BenchmarkScenario
            {
                BenchmarkId = benchmarkId,
                Name = name,
                Description = description,
                Recipe = recipe,
                SuccessCriteria = successCriteria,
                Difficulty = difficulty,
                Categories = new List<string> { "generated" },
                Version = "1.0.0"
            };
        }

        /// <summary>
        /// Generates variations of a benchmark for stress testing.
        /// </summary>
        public static List<BenchmarkScenario> GenerateVariations(
            BenchmarkScenario baseBenchmark,
            int variationCount,
            int? seedOffset = null)
        {
            var variations = new List<BenchmarkScenario>();

            for (int i = 0; i < variationCount; i++)
            {
                var seed = baseBenchmark.Recipe.Seed.HasValue
                    ? baseBenchmark.Recipe.Seed.Value + (seedOffset ?? 1) + i
                    : null as int?;

                var variation = new BenchmarkScenario
                {
                    BenchmarkId = $"{baseBenchmark.BenchmarkId}_var_{i + 1}",
                    Name = $"{baseBenchmark.Name} (Variation {i + 1})",
                    Description = $"{baseBenchmark.Description} - Variation {i + 1}",
                    Recipe = new BenchmarkRecipe
                    {
                        Generator = baseBenchmark.Recipe.Generator,
                        Template = baseBenchmark.Recipe.Template,
                        Seed = seed,
                        GeneratorVersion = baseBenchmark.Recipe.GeneratorVersion,
                        Width = baseBenchmark.Recipe.Width,
                        Height = baseBenchmark.Recipe.Height,
                        Levels = baseBenchmark.Recipe.Levels,
                        Parameters = new Dictionary<string, string>(baseBenchmark.Recipe.Parameters)
                    },
                    SuccessCriteria = new SuccessCriteria
                    {
                        Type = baseBenchmark.SuccessCriteria.Type,
                        GoalLocation = baseBenchmark.SuccessCriteria.GoalLocation != null
                            ? new WorldLocation
                            {
                                X = baseBenchmark.SuccessCriteria.GoalLocation.X,
                                Y = baseBenchmark.SuccessCriteria.GoalLocation.Y,
                                Z = baseBenchmark.SuccessCriteria.GoalLocation.Z
                            }
                            : null,
                        RequiredItems = new List<string>(baseBenchmark.SuccessCriteria.RequiredItems),
                        MinSurvivalTurns = baseBenchmark.SuccessCriteria.MinSurvivalTurns,
                        MaxSteps = baseBenchmark.SuccessCriteria.MaxSteps,
                        MaxTimeSeconds = baseBenchmark.SuccessCriteria.MaxTimeSeconds,
                        CustomCriteria = new Dictionary<string, object>(baseBenchmark.SuccessCriteria.CustomCriteria)
                    },
                    Difficulty = baseBenchmark.Difficulty,
                    Categories = new List<string>(baseBenchmark.Categories) { "variation" },
                    Version = baseBenchmark.Version
                };

                variations.Add(variation);
            }

            return variations;
        }

        /// <summary>
        /// Generates edge case scenarios based on failure patterns.
        /// </summary>
        public static BenchmarkScenario GenerateEdgeCase(
            string benchmarkId,
            string failurePattern,
            BenchmarkRecipe baseRecipe)
        {
            var recipe = new BenchmarkRecipe
            {
                Generator = baseRecipe.Generator,
                Template = baseRecipe.Template,
                Seed = baseRecipe.Seed,
                GeneratorVersion = baseRecipe.GeneratorVersion,
                Width = baseRecipe.Width,
                Height = baseRecipe.Height,
                Levels = baseRecipe.Levels,
                Parameters = new Dictionary<string, string>(baseRecipe.Parameters)
            };

            // Adjust recipe based on failure pattern
            if (failurePattern.Contains("navigation", StringComparison.OrdinalIgnoreCase))
            {
                // Generate harder navigation challenge
                recipe.Width = Math.Max(30, recipe.Width + 20);
                recipe.Height = Math.Max(30, recipe.Height + 20);
                recipe.Parameters["minBranchingFactor"] = "0.1";
                recipe.Parameters["maxBranchingFactor"] = "0.3";
            }
            else if (failurePattern.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                     failurePattern.Contains("lock", StringComparison.OrdinalIgnoreCase))
            {
                // Generate harder key-lock chain
                recipe.Parameters["keyLockChainDepth"] = "3";
                recipe.Parameters["minRooms"] = "6";
                recipe.Parameters["maxRooms"] = "10";
            }
            else if (failurePattern.Contains("trap", StringComparison.OrdinalIgnoreCase))
            {
                // Generate more traps
                recipe.Parameters["trapDensity"] = "0.3";
            }
            else if (failurePattern.Contains("perception", StringComparison.OrdinalIgnoreCase))
            {
                // Generate smaller map with more entities
                recipe.Width = Math.Max(30, recipe.Width - 20);
                recipe.Height = Math.Max(30, recipe.Height - 20);
                recipe.Parameters["entityDensity"] = "0.5";
            }

            return new BenchmarkScenario
            {
                BenchmarkId = benchmarkId,
                Name = $"Edge Case: {failurePattern}",
                Description = $"Edge case scenario targeting {failurePattern}",
                Recipe = recipe,
                SuccessCriteria = new SuccessCriteria
                {
                    Type = SuccessCriteriaType.CompleteWithinLimits.ToString(),
                    MaxSteps = 200
                },
                Difficulty = 8,
                Categories = new List<string> { "edge-case", failurePattern.ToLowerInvariant() },
                Version = "1.0.0"
            };
        }
    }
}

