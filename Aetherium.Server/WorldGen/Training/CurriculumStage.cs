using System;
using System.Collections.Generic;

namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Represents a single stage in a training curriculum with specific difficulty parameters.
    /// </summary>
    public sealed class CurriculumStage
    {
        /// <summary>
        /// Unique identifier for this stage within the curriculum.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the stage.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this stage teaches.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Difficulty level (0-100).
        /// </summary>
        public int Difficulty { get; set; } = 50;

        /// <summary>
        /// Prerequisites that must be met before advancing to this stage.
        /// </summary>
        public PrerequisiteRequirements Prerequisites { get; set; } = new PrerequisiteRequirements();

        /// <summary>
        /// World generation parameters for this stage.
        /// </summary>
        public StageParameters Parameters { get; set; } = new StageParameters();

        /// <summary>
        /// Criteria for completing this stage.
        /// </summary>
        public CompletionCriteria CompletionCriteria { get; set; } = new CompletionCriteria();
    }

    /// <summary>
    /// Requirements that must be met before advancing to a stage.
    /// </summary>
    public sealed class PrerequisiteRequirements
    {
        /// <summary>
        /// Previous stages that must be completed.
        /// </summary>
        public List<string> RequiredStageIds { get; set; } = new List<string>();

        /// <summary>
        /// Minimum success rate in previous stages (0-1).
        /// </summary>
        public double? MinSuccessRate { get; set; }

        /// <summary>
        /// Minimum number of completed runs.
        /// </summary>
        public int? MinCompletedRuns { get; set; }

        /// <summary>
        /// Minimum skill level required.
        /// </summary>
        public int? MinSkillLevel { get; set; }
    }

    /// <summary>
    /// World generation parameters specific to a curriculum stage.
    /// </summary>
    public sealed class StageParameters
    {
        /// <summary>
        /// Map dimensions.
        /// </summary>
        public int Width { get; set; } = 60;

        public int Height { get; set; } = 60;

        public int Levels { get; set; } = 1;

        /// <summary>
        /// Trap density (0-1).
        /// </summary>
        public double TrapDensity { get; set; } = 0.1;

        /// <summary>
        /// Enemy count.
        /// </summary>
        public int EnemyCount { get; set; } = 0;

        /// <summary>
        /// Puzzle complexity (0-1).
        /// </summary>
        public double PuzzleComplexity { get; set; } = 0.2;

        /// <summary>
        /// Key-lock chain depth (number of sequential key-lock pairs).
        /// </summary>
        public int KeyLockChainDepth { get; set; } = 1;

        /// <summary>
        /// Secret room density (0-1).
        /// </summary>
        public double SecretRoomDensity { get; set; } = 0.05;

        /// <summary>
        /// Room count range.
        /// </summary>
        public int MinRooms { get; set; } = 3;

        public int MaxRooms { get; set; } = 8;

        /// <summary>
        /// Branching factor range (0-1).
        /// </summary>
        public double MinBranchingFactor { get; set; } = 0.3;

        public double MaxBranchingFactor { get; set; } = 0.7;

        /// <summary>
        /// Resource availability (0-1, higher = more resources).
        /// </summary>
        public double ResourceAvailability { get; set; } = 0.5;

        /// <summary>
        /// Combat difficulty (0-1).
        /// </summary>
        public double CombatDifficulty { get; set; } = 0.3;

        /// <summary>
        /// Additional generator parameters.
        /// </summary>
        public Dictionary<string, string> AdditionalParameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Criteria that determine when a stage is considered complete.
    /// </summary>
    public sealed class CompletionCriteria
    {
        /// <summary>
        /// Minimum success rate required (0-1).
        /// </summary>
        public double? MinSuccessRate { get; set; }

        /// <summary>
        /// Minimum number of successful completions.
        /// </summary>
        public int MinSuccessfulCompletions { get; set; } = 3;

        /// <summary>
        /// Minimum number of total attempts.
        /// </summary>
        public int MinAttempts { get; set; } = 5;

        /// <summary>
        /// Maximum average steps allowed.
        /// </summary>
        public int? MaxAverageSteps { get; set; }

        /// <summary>
        /// Minimum average efficiency score (0-1).
        /// </summary>
        public double? MinEfficiency { get; set; }
    }
}

