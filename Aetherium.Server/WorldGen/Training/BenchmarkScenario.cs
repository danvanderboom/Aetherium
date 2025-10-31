using System;
using System.Collections.Generic;

namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Defines a benchmark scenario for evaluating agent performance.
    /// </summary>
    public sealed class BenchmarkScenario
    {
        /// <summary>
        /// Unique identifier for this benchmark.
        /// </summary>
        public string BenchmarkId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the benchmark.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this benchmark tests.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category/tags for organizing benchmarks.
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Difficulty rating (1-10).
        /// </summary>
        public int Difficulty { get; set; } = 5;

        /// <summary>
        /// Version for regression tracking.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// World generation recipe for this benchmark.
        /// </summary>
        public BenchmarkRecipe Recipe { get; set; } = new BenchmarkRecipe();

        /// <summary>
        /// Success criteria that determine if the agent passed.
        /// </summary>
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
        public string Generator { get; set; } = "AdvancedDungeon";

        /// <summary>
        /// Template type (e.g., "dungeon", "outdoor").
        /// </summary>
        public string Template { get; set; } = "dungeon";

        /// <summary>
        /// Seed for deterministic generation (null for random).
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Generator version.
        /// </summary>
        public string GeneratorVersion { get; set; } = "1.0.0";

        /// <summary>
        /// World dimensions.
        /// </summary>
        public int Width { get; set; } = 60;

        public int Height { get; set; } = 60;

        public int Levels { get; set; } = 1;

        /// <summary>
        /// Generator-specific parameters.
        /// </summary>
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
        public SuccessCriteriaType Type { get; set; } = SuccessCriteriaType.ReachGoal;

        /// <summary>
        /// Target location to reach (for ReachGoal type).
        /// </summary>
        public WorldLocation? GoalLocation { get; set; }

        /// <summary>
        /// Items that must be collected (for CollectItems type).
        /// </summary>
        public List<string> RequiredItems { get; set; } = new List<string>();

        /// <summary>
        /// Minimum number of turns to survive (for SurviveTurns type).
        /// </summary>
        public int? MinSurvivalTurns { get; set; }

        /// <summary>
        /// Maximum number of steps allowed.
        /// </summary>
        public int? MaxSteps { get; set; }

        /// <summary>
        /// Maximum time allowed (seconds).
        /// </summary>
        public double? MaxTimeSeconds { get; set; }

        /// <summary>
        /// Additional custom criteria parameters.
        /// </summary>
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
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

