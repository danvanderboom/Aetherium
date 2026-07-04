using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aetherium.Model.Training
{
    /// <summary>
    /// Defines a benchmark scenario for evaluating agent performance.
    /// </summary>
    public sealed class BenchmarkScenario
    {
        /// <summary>
        /// Unique identifier for this benchmark.
        /// </summary>
        [JsonPropertyName("benchmarkId")]
        public string BenchmarkId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the benchmark.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this benchmark tests.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category/tags for organizing benchmarks.
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Difficulty rating (1-10).
        /// </summary>
        [JsonPropertyName("difficulty")]
        public int Difficulty { get; set; } = 5;

        /// <summary>
        /// Version for regression tracking.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// World generation recipe for this benchmark.
        /// </summary>
        [JsonPropertyName("recipe")]
        public BenchmarkRecipe Recipe { get; set; } = new BenchmarkRecipe();

        /// <summary>
        /// Success criteria that determine if the agent passed.
        /// </summary>
        [JsonPropertyName("successCriteria")]
        public SuccessCriteria SuccessCriteria { get; set; } = new SuccessCriteria();
    }

    /// <summary>
    /// Recipe for generating the benchmark world.
    /// </summary>
    public sealed class BenchmarkRecipe
    {
        /// <summary>
        /// Generator name (e.g., "AdvancedDungeon", "PerlinTerrain").
        /// </summary>
        [JsonPropertyName("generator")]
        public string Generator { get; set; } = "AdvancedDungeon";

        /// <summary>
        /// Template type (e.g., "dungeon", "outdoor").
        /// </summary>
        [JsonPropertyName("template")]
        public string Template { get; set; } = "dungeon";

        /// <summary>
        /// Seed for deterministic generation (null for random).
        /// </summary>
        [JsonPropertyName("seed")]
        public int? Seed { get; set; }

        /// <summary>
        /// Generator version.
        /// </summary>
        [JsonPropertyName("generatorVersion")]
        public string GeneratorVersion { get; set; } = "1.0.0";

        /// <summary>
        /// World dimensions.
        /// </summary>
        [JsonPropertyName("width")]
        public int Width { get; set; } = 60;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 60;

        [JsonPropertyName("levels")]
        public int Levels { get; set; } = 1;

        /// <summary>
        /// Generator-specific parameters.
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Criteria that determine if a benchmark run was successful.
    /// </summary>
    public sealed class SuccessCriteria
    {
        /// <summary>
        /// Type of success criteria.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = SuccessCriteriaType.ReachGoal.ToString();

        /// <summary>
        /// Target location to reach (for ReachGoal type).
        /// </summary>
        [JsonPropertyName("goalLocation")]
        public WorldLocation? GoalLocation { get; set; }

        /// <summary>
        /// Items that must be collected (for CollectItems type).
        /// </summary>
        [JsonPropertyName("requiredItems")]
        public List<string> RequiredItems { get; set; } = new List<string>();

        /// <summary>
        /// Minimum number of turns to survive (for SurviveTurns type).
        /// </summary>
        [JsonPropertyName("minSurvivalTurns")]
        public int? MinSurvivalTurns { get; set; }

        /// <summary>
        /// Maximum number of steps allowed.
        /// </summary>
        [JsonPropertyName("maxSteps")]
        public int? MaxSteps { get; set; }

        /// <summary>
        /// Maximum time allowed (seconds).
        /// </summary>
        [JsonPropertyName("maxTimeSeconds")]
        public double? MaxTimeSeconds { get; set; }

        /// <summary>
        /// Additional custom criteria parameters.
        /// </summary>
        [JsonPropertyName("customCriteria")]
        public Dictionary<string, object> CustomCriteria { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Types of success criteria.
    /// </summary>
    public enum SuccessCriteriaType
    {
        /// <summary>
        /// Agent must reach a specific goal location.
        /// </summary>
        ReachGoal,

        /// <summary>
        /// Agent must collect specific items.
        /// </summary>
        CollectItems,

        /// <summary>
        /// Agent must survive for a minimum number of turns.
        /// </summary>
        SurviveTurns,

        /// <summary>
        /// Agent must complete within step/time limits.
        /// </summary>
        CompleteWithinLimits
    }

    /// <summary>
    /// Simple location representation.
    /// </summary>
    public sealed class WorldLocation
    {
        [JsonPropertyName("x")]
        public int X { get; set; }
        [JsonPropertyName("y")]
        public int Y { get; set; }
        [JsonPropertyName("z")]
        public int Z { get; set; }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

