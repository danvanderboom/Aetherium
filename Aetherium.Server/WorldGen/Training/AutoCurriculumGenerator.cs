using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Telemetry;

namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Automatically generates curriculum stages based on agent performance analysis.
    /// </summary>
    public static class AutoCurriculumGenerator
    {
        /// <summary>
        /// Generates the next curriculum stage based on agent performance.
        /// </summary>
        public static CurriculumStage GenerateNextStage(
            PerformanceAnalysis currentPerformance,
            CurriculumStage? currentStage,
            int agentSkillLevel)
        {
            if (currentStage == null)
            {
                // Generate initial stage
                return CreateInitialStage();
            }

            var analysis = currentPerformance;
            var currentDifficulty = currentStage.Difficulty;

            // Determine difficulty adjustment
            int nextDifficulty = currentDifficulty;
            
            if (analysis.SuccessRate >= 0.8 && analysis.TotalSteps >= 20)
            {
                // Agent is performing well - increase difficulty
                nextDifficulty = Math.Min(100, currentDifficulty + 10);
            }
            else if (analysis.SuccessRate < 0.4 && analysis.TotalSteps >= 20)
            {
                // Agent is struggling - decrease difficulty
                nextDifficulty = Math.Max(0, currentDifficulty - 10);
            }
            else if (analysis.SuccessRate >= 0.6)
            {
                // Moderate performance - small increase
                nextDifficulty = Math.Min(100, currentDifficulty + 5);
            }

            // Identify specific weaknesses and adjust parameters
            var stageParams = AdjustParametersForWeaknesses(currentStage.Parameters, analysis);

            var nextStage = new CurriculumStage
            {
                StageId = $"auto_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Name = $"Auto-Generated Stage (Difficulty {nextDifficulty})",
                Description = $"Automatically generated based on performance analysis. Previous success rate: {analysis.SuccessRate:P1}",
                Difficulty = nextDifficulty,
                Prerequisites = new PrerequisiteRequirements
                {
                    RequiredStageIds = new List<string> { currentStage.StageId },
                    MinSuccessRate = 0.5,
                    MinCompletedRuns = 3
                },
                Parameters = stageParams,
                CompletionCriteria = new CompletionCriteria
                {
                    MinSuccessRate = 0.7,
                    MinSuccessfulCompletions = 3,
                    MinAttempts = 5
                }
            };

            return nextStage;
        }

        private static CurriculumStage CreateInitialStage()
        {
            return new CurriculumStage
            {
                StageId = "initial",
                Name = "Initial Training Stage",
                Description = "Starting point for agent training",
                Difficulty = 20,
                Prerequisites = new PrerequisiteRequirements(),
                Parameters = new StageParameters
                {
                    Width = 40,
                    Height = 40,
                    Levels = 1,
                    TrapDensity = 0.0,
                    EnemyCount = 0,
                    PuzzleComplexity = 0.1,
                    KeyLockChainDepth = 0,
                    SecretRoomDensity = 0.0,
                    MinRooms = 3,
                    MaxRooms = 5,
                    MinBranchingFactor = 0.2,
                    MaxBranchingFactor = 0.4,
                    ResourceAvailability = 0.8,
                    CombatDifficulty = 0.0
                },
                CompletionCriteria = new CompletionCriteria
                {
                    MinSuccessRate = 0.6,
                    MinSuccessfulCompletions = 3,
                    MinAttempts = 5
                }
            };
        }

        private static StageParameters AdjustParametersForWeaknesses(
            StageParameters currentParams,
            PerformanceAnalysis analysis)
        {
            var adjusted = new StageParameters
            {
                Width = currentParams.Width,
                Height = currentParams.Height,
                Levels = currentParams.Levels,
                TrapDensity = currentParams.TrapDensity,
                EnemyCount = currentParams.EnemyCount,
                PuzzleComplexity = currentParams.PuzzleComplexity,
                KeyLockChainDepth = currentParams.KeyLockChainDepth,
                SecretRoomDensity = currentParams.SecretRoomDensity,
                MinRooms = currentParams.MinRooms,
                MaxRooms = currentParams.MaxRooms,
                MinBranchingFactor = currentParams.MinBranchingFactor,
                MaxBranchingFactor = currentParams.MaxBranchingFactor,
                ResourceAvailability = currentParams.ResourceAvailability,
                CombatDifficulty = currentParams.CombatDifficulty,
                AdditionalParameters = new Dictionary<string, string>(currentParams.AdditionalParameters)
            };

            // Adjust based on identified weaknesses
            foreach (var weakness in analysis.IdentifiedWeaknesses)
            {
                if (weakness.Contains("failure rate") && weakness.Contains("move"))
                {
                    // Navigation issues - reduce complexity
                    adjusted.MinRooms = Math.Max(2, adjusted.MinRooms - 1);
                    adjusted.MaxRooms = Math.Max(3, adjusted.MaxRooms - 1);
                }
                else if (weakness.Contains("keys") || weakness.Contains("locks"))
                {
                    // Key-lock issues - reduce chain depth
                    adjusted.KeyLockChainDepth = Math.Max(0, adjusted.KeyLockChainDepth - 1);
                }
                else if (weakness.Contains("trap"))
                {
                    // Trap issues - reduce density
                    adjusted.TrapDensity = Math.Max(0, adjusted.TrapDensity - 0.1);
                }
                else if (weakness.Contains("perception complexity"))
                {
                    // Perception overload - reduce map size
                    adjusted.Width = Math.Max(30, adjusted.Width - 10);
                    adjusted.Height = Math.Max(30, adjusted.Height - 10);
                }
            }

            return adjusted;
        }

        /// <summary>
        /// Determines if the agent is ready to progress to the next stage.
        /// </summary>
        public static bool IsReadyForNextStage(
            CurriculumStage currentStage,
            PerformanceAnalysis performance,
            int completedRuns,
            int totalAttempts)
        {
            var criteria = currentStage.CompletionCriteria;

            // Check minimum attempts
            if (totalAttempts < criteria.MinAttempts)
            {
                return false;
            }

            // Check minimum successful completions
            if (completedRuns < criteria.MinSuccessfulCompletions)
            {
                return false;
            }

            // Check success rate
            if (criteria.MinSuccessRate.HasValue)
            {
                if (performance.SuccessRate < criteria.MinSuccessRate.Value)
                {
                    return false;
                }
            }

            // Check average steps
            if (criteria.MaxAverageSteps.HasValue)
            {
                // Would need step count from performance - simplified check
                // In production, would track average steps per successful run
            }

            // Check efficiency
            if (criteria.MinEfficiency.HasValue)
            {
                // Would need efficiency metric from performance
                // Simplified: assume efficiency based on success rate
                if (performance.SuccessRate < criteria.MinEfficiency.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

