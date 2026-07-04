using System;
using System.Collections.Generic;
using Orleans;
using System.Text.Json.Serialization;

namespace Aetherium.Model.Training
{
    /// <summary>
    /// Represents a single stage in a training curriculum with specific difficulty parameters.
    /// </summary>
    [GenerateSerializer]
    public sealed class CurriculumStage
    {
        /// <summary>
        /// Unique identifier for this stage within the curriculum.
        /// </summary>
        [Id(0)]
        [JsonPropertyName("stageId")]
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the stage.
        /// </summary>
        [Id(1)]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this stage teaches.
        /// </summary>
        [Id(2)]
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Difficulty level (0-100).
        /// </summary>
        [Id(3)]
        [JsonPropertyName("difficulty")]
        public int Difficulty { get; set; } = 50;

        /// <summary>
        /// Prerequisites that must be met before advancing to this stage.
        /// </summary>
        [Id(4)]
        [JsonPropertyName("prerequisites")]
        public PrerequisiteRequirements Prerequisites { get; set; } = new PrerequisiteRequirements();

        /// <summary>
        /// World generation parameters for this stage.
        /// </summary>
        [Id(5)]
        [JsonPropertyName("parameters")]
        public StageParameters Parameters { get; set; } = new StageParameters();

        /// <summary>
        /// Criteria for completing this stage.
        /// </summary>
        [Id(6)]
        [JsonPropertyName("completionCriteria")]
        public CompletionCriteria CompletionCriteria { get; set; } = new CompletionCriteria();
    }

    /// <summary>
    /// Requirements that must be met before advancing to a stage.
    /// </summary>
    [GenerateSerializer]
    public sealed class PrerequisiteRequirements
    {
        /// <summary>
        /// Previous stages that must be completed.
        /// </summary>
        [Id(0)]
        [JsonPropertyName("requiredStageIds")]
        public List<string> RequiredStageIds { get; set; } = new List<string>();

        /// <summary>
        /// Minimum success rate in previous stages (0-1).
        /// </summary>
        [Id(1)]
        [JsonPropertyName("minSuccessRate")]
        public double? MinSuccessRate { get; set; }

        /// <summary>
        /// Minimum number of completed runs.
        /// </summary>
        [Id(2)]
        [JsonPropertyName("minCompletedRuns")]
        public int? MinCompletedRuns { get; set; }

        /// <summary>
        /// Minimum skill level required.
        /// </summary>
        [Id(3)]
        [JsonPropertyName("minSkillLevel")]
        public int? MinSkillLevel { get; set; }
    }

    /// <summary>
    /// World generation parameters specific to a curriculum stage.
    /// </summary>
    [GenerateSerializer]
    public sealed class StageParameters
    {
        /// <summary>
        /// Map dimensions.
        /// </summary>
        [Id(0)]
        [JsonPropertyName("width")]
        [JsonConverter(typeof(JsonStringToIntConverter))]
        public int Width { get; set; } = 60;

        [Id(1)]
        [JsonPropertyName("height")]
        [JsonConverter(typeof(JsonStringToIntConverter))]
        public int Height { get; set; } = 60;

        [Id(2)]
        [JsonPropertyName("levels")]
        [JsonConverter(typeof(JsonStringToIntConverter))]
        public int Levels { get; set; } = 1;

        /// <summary>
        /// Trap density (0-1).
        /// </summary>
        [Id(3)]
        [JsonPropertyName("trapDensity")]
        public double TrapDensity { get; set; } = 0.1;

        /// <summary>
        /// Enemy count.
        /// </summary>
        [Id(4)]
        [JsonPropertyName("enemyCount")]
        public int EnemyCount { get; set; } = 0;

        /// <summary>
        /// Puzzle complexity (0-1).
        /// </summary>
        [Id(5)]
        [JsonPropertyName("puzzleComplexity")]
        public double PuzzleComplexity { get; set; } = 0.2;

        /// <summary>
        /// Key-lock chain depth (number of sequential key-lock pairs).
        /// </summary>
        [Id(6)]
        [JsonPropertyName("keyLockChainDepth")]
        public int KeyLockChainDepth { get; set; } = 1;

        /// <summary>
        /// Secret room density (0-1).
        /// </summary>
        [Id(7)]
        [JsonPropertyName("secretRoomDensity")]
        public double SecretRoomDensity { get; set; } = 0.05;

        /// <summary>
        /// Room count range.
        /// </summary>
        [Id(8)]
        [JsonPropertyName("minRooms")]
        public int MinRooms { get; set; } = 3;

        [Id(9)]
        [JsonPropertyName("maxRooms")]
        public int MaxRooms { get; set; } = 8;

        /// <summary>
        /// Branching factor range (0-1).
        /// </summary>
        [Id(10)]
        [JsonPropertyName("minBranchingFactor")]
        public double MinBranchingFactor { get; set; } = 0.3;

        [Id(11)]
        [JsonPropertyName("maxBranchingFactor")]
        public double MaxBranchingFactor { get; set; } = 0.7;

        /// <summary>
        /// Resource availability (0-1, higher = more resources).
        /// </summary>
        [Id(12)]
        [JsonPropertyName("resourceAvailability")]
        public double ResourceAvailability { get; set; } = 0.5;

        /// <summary>
        /// Combat difficulty (0-1).
        /// </summary>
        [Id(13)]
        [JsonPropertyName("combatDifficulty")]
        public double CombatDifficulty { get; set; } = 0.3;

        /// <summary>
        /// Additional generator parameters.
        /// </summary>
        [Id(14)]
        [JsonPropertyName("additionalParameters")]
        public Dictionary<string, string> AdditionalParameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Criteria that determine when a stage is considered complete.
    /// </summary>
    [GenerateSerializer]
    public sealed class CompletionCriteria
    {
        /// <summary>
        /// Minimum success rate required (0-1).
        /// </summary>
        [Id(0)]
        [JsonPropertyName("minSuccessRate")]
        public double? MinSuccessRate { get; set; }

        /// <summary>
        /// Minimum number of successful completions.
        /// </summary>
        [Id(1)]
        [JsonPropertyName("minSuccessfulCompletions")]
        public int MinSuccessfulCompletions { get; set; } = 3;

        /// <summary>
        /// Minimum number of total attempts.
        /// </summary>
        [Id(2)]
        [JsonPropertyName("minAttempts")]
        public int MinAttempts { get; set; } = 5;

        /// <summary>
        /// Maximum average steps allowed.
        /// </summary>
        [Id(3)]
        [JsonPropertyName("maxAverageSteps")]
        public int? MaxAverageSteps { get; set; }

        /// <summary>
        /// Minimum average efficiency score (0-1).
        /// </summary>
        [Id(4)]
        [JsonPropertyName("minEfficiency")]
        public double? MinEfficiency { get; set; }
    }
}

