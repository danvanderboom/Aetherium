using System;
using System.Collections.Generic;

using Aetherium.Model.Training;
namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Represents a difficulty profile with numeric difficulty score (0-100) and training-specific metrics.
    /// </summary>
    public sealed class DifficultyProfile
    {
        /// <summary>
        /// Overall difficulty score (0-100, where 0 is easiest, 100 is hardest).
        /// </summary>
        public int DifficultyScore { get; set; } = 50;

        /// <summary>
        /// Component difficulty scores for different aspects.
        /// </summary>
        public DifficultyComponents Components { get; set; } = new DifficultyComponents();

        /// <summary>
        /// Predicted agent success rate (0-1) based on structural metrics.
        /// </summary>
        public double? PredictedSuccessRate { get; set; }

        /// <summary>
        /// Training mode metrics (heatmaps, performance tracking).
        /// </summary>
        public TrainingMetrics TrainingMetrics { get; set; } = new TrainingMetrics();

        /// <summary>
        /// Calculates overall difficulty score from components.
        /// </summary>
        public void CalculateDifficultyScore()
        {
            var components = Components;
            
            // Weighted average of component difficulties
            var totalWeight = 0.0;
            var weightedSum = 0.0;

            // Navigation complexity (based on map size, branching, loops)
            var navigationWeight = 0.25;
            var navigationDifficulty = CalculateNavigationDifficulty(components);
            totalWeight += navigationWeight;
            weightedSum += navigationDifficulty * navigationWeight;

            // Puzzle complexity (keys, locks, secrets)
            var puzzleWeight = 0.20;
            var puzzleDifficulty = CalculatePuzzleDifficulty(components);
            totalWeight += puzzleWeight;
            weightedSum += puzzleDifficulty * puzzleWeight;

            // Combat difficulty (enemies, traps)
            var combatWeight = 0.25;
            var combatDifficulty = CalculateCombatDifficulty(components);
            totalWeight += combatWeight;
            weightedSum += combatDifficulty * combatWeight;

            // Resource scarcity
            var resourceWeight = 0.15;
            var resourceDifficulty = (1.0 - components.ResourceAvailability) * 100.0;
            totalWeight += resourceWeight;
            weightedSum += resourceDifficulty * resourceWeight;

            // Map complexity (size, levels, rooms)
            var mapWeight = 0.15;
            var mapDifficulty = CalculateMapComplexity(components);
            totalWeight += mapWeight;
            weightedSum += mapDifficulty * mapWeight;

            DifficultyScore = (int)Math.Round(Math.Clamp(weightedSum / totalWeight, 0, 100));
        }

        private double CalculateNavigationDifficulty(DifficultyComponents components)
        {
            // Based on branching factor, loop ratio, dead-end count
            var branchingFactor = components.BranchingFactor;
            var loopRatio = components.LoopRatio;
            var deadEndRatio = components.DeadEndCount / (double)Math.Max(1, components.TotalRooms);

            // More branching = harder navigation
            // More loops = easier navigation (more paths)
            // More dead ends = harder navigation
            var difficulty = (branchingFactor * 30.0) + ((1.0 - loopRatio) * 40.0) + (deadEndRatio * 30.0);

            return Math.Clamp(difficulty, 0, 100);
        }

        private double CalculatePuzzleDifficulty(DifficultyComponents components)
        {
            // Based on key-lock chain depth, secret room density, puzzle complexity
            var chainDifficulty = Math.Min(components.KeyLockChainDepth * 20.0, 50.0);
            var secretDifficulty = components.SecretRoomDensity * 30.0;
            var puzzleDifficulty = components.PuzzleComplexity * 20.0;

            return Math.Clamp(chainDifficulty + secretDifficulty + puzzleDifficulty, 0, 100);
        }

        private double CalculateCombatDifficulty(DifficultyComponents components)
        {
            // Based on enemy count, trap density, combat difficulty setting
            var enemyDifficulty = Math.Min(components.EnemyCount * 10.0, 40.0);
            var trapDifficulty = components.TrapDensity * 30.0;
            var combatDifficulty = components.CombatDifficulty * 30.0;

            return Math.Clamp(enemyDifficulty + trapDifficulty + combatDifficulty, 0, 100);
        }

        private double CalculateMapComplexity(DifficultyComponents components)
        {
            // Based on map size, number of levels, room count
            var sizeFactor = Math.Min((components.Width * components.Height) / 10000.0, 1.0) * 40.0;
            var levelFactor = (components.Levels - 1) * 20.0;
            var roomFactor = Math.Min(components.TotalRooms / 20.0, 1.0) * 40.0;

            return Math.Clamp(sizeFactor + levelFactor + roomFactor, 0, 100);
        }

        /// <summary>
        /// Predicts agent success rate based on structural metrics.
        /// </summary>
        public void PredictSuccessRate()
        {
            // Simple heuristic: inverse relationship with difficulty
            // Lower difficulty = higher predicted success rate
            var baseSuccessRate = 1.0 - (DifficultyScore / 100.0);

            // Adjust based on specific metrics
            var components = Components;

            // High branching factor = harder navigation = lower success
            if (components.BranchingFactor > 0.7)
            {
                baseSuccessRate *= 0.9;
            }

            // Long key-lock chains = harder puzzles = lower success
            if (components.KeyLockChainDepth > 2)
            {
                baseSuccessRate *= 0.85;
            }

            // Many enemies = harder combat = lower success
            if (components.EnemyCount > 5)
            {
                baseSuccessRate *= 0.8;
            }

            // Very low resource availability = harder survival = lower success
            if (components.ResourceAvailability < 0.3)
            {
                baseSuccessRate *= 0.75;
            }

            PredictedSuccessRate = Math.Clamp(baseSuccessRate, 0, 1);
        }
    }

    /// <summary>
    /// Component difficulty scores for different aspects.
    /// </summary>
    public sealed class DifficultyComponents
    {
        /// <summary>
        /// Map dimensions.
        /// </summary>
        public int Width { get; set; }

        public int Height { get; set; }

        public int Levels { get; set; }

        /// <summary>
        /// Navigation complexity metrics.
        /// </summary>
        public double BranchingFactor { get; set; }

        public double LoopRatio { get; set; }

        public int DeadEndCount { get; set; }

        public int TotalRooms { get; set; }

        /// <summary>
        /// Puzzle complexity metrics.
        /// </summary>
        public int KeyLockChainDepth { get; set; }

        public double SecretRoomDensity { get; set; }

        public double PuzzleComplexity { get; set; }

        /// <summary>
        /// Combat difficulty metrics.
        /// </summary>
        public int EnemyCount { get; set; }

        public double TrapDensity { get; set; }

        public double CombatDifficulty { get; set; }

        /// <summary>
        /// Resource availability (0-1, higher = more resources).
        /// </summary>
        public double ResourceAvailability { get; set; } = 0.5;
    }

    /// <summary>
    /// Training-specific metrics for performance analysis.
    /// </summary>
    public sealed class TrainingMetrics
    {
        /// <summary>
        /// Whether heatmap data collection is enabled.
        /// </summary>
        public bool HeatmapEnabled { get; set; } = false;

        /// <summary>
        /// Heatmap data: counts of agent visits per location.
        /// </summary>
        public Dictionary<string, int> HeatmapData { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Failure pattern analysis: common failure points.
        /// </summary>
        public List<string> FailurePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Performance metrics collected during training runs.
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new Dictionary<string, double>();
    }
}

