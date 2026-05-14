using System;
using System.Collections.Generic;
using Aetherium.WorldGen.Training;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Captures quantitative metrics collected during procedural generation
    /// to support validation, regression testing, and analytics.
    /// </summary>
    public sealed class GenerationMetrics
    {
        private readonly Dictionary<int, int> _pathLengthHistogram = new();
        private readonly Dictionary<string, double> _biomeCoverage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _phaseDurationsMs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _customMetrics = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _validationFailures = new();

        /// <summary>
        /// The effective seed used to initialize the random number generator for this generation run.
        /// Persist or log this value to replay an interesting world with the same seed.
        /// </summary>
        public int EffectiveSeed { get; set; }

        public double BranchingFactor { get; set; }
        public double LoopRatio { get; set; }
        public int DeadEndCount { get; set; }
        public int Rooms { get; set; }
        public int Corridors { get; set; }
        public int SecretsPlaced { get; set; }
        public int LockedDoors { get; set; }
        public int KeysPlaced { get; set; }
        public int TrapsPlaced { get; set; }
        public int AlternateSolutions { get; set; }
        public bool ValidationPassed { get; set; }

        /// <summary>
        /// Difficulty profile for training scenarios.
        /// </summary>
        public DifficultyProfile? DifficultyProfile { get; set; }

        /// <summary>
        /// Predicted agent success rate based on structural metrics (0-1).
        /// </summary>
        public double? PredictedAgentSuccessRate { get; set; }

        public IReadOnlyDictionary<int, int> PathLengthHistogram => _pathLengthHistogram;
        public IReadOnlyDictionary<string, double> BiomeCoverage => _biomeCoverage;
        public IReadOnlyDictionary<string, double> PhaseDurationsMs => _phaseDurationsMs;
        public IReadOnlyDictionary<string, double> CustomMetrics => _customMetrics;
        public IReadOnlyList<string> ValidationFailures => _validationFailures;

        public void RecordPhaseDuration(string phaseName, double milliseconds)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                return;

            _phaseDurationsMs[phaseName] = milliseconds;
        }

        public void IncrementPathLength(int length)
        {
            if (length < 0)
                return;

            if (_pathLengthHistogram.TryGetValue(length, out var count))
            {
                _pathLengthHistogram[length] = count + 1;
            }
            else
            {
                _pathLengthHistogram[length] = 1;
            }
        }

        public void RecordBiomeCoverage(string biomeName, double normalizedCoverage)
        {
            if (string.IsNullOrWhiteSpace(biomeName))
                return;

            _biomeCoverage[biomeName] = normalizedCoverage;
        }

        public void AddValidationFailure(string failure)
        {
            if (string.IsNullOrWhiteSpace(failure))
                return;

            _validationFailures.Add(failure);
            ValidationPassed = false;
        }

        public void ClearValidationFailures()
        {
            _validationFailures.Clear();
        }

        /// <summary>
        /// Sets or updates a custom numeric metric by key.
        /// </summary>
        public void SetMetric(string key, double value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _customMetrics[key] = value;
        }

        /// <summary>
        /// Gets a custom numeric metric by key if it exists.
        /// </summary>
        public bool TryGetMetric(string key, out double value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = 0;
                return false;
            }

            return _customMetrics.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns true if a custom metric with the given key exists.
        /// </summary>
        public bool HasMetric(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _customMetrics.ContainsKey(key);
        }

        /// <summary>
        /// Calculates difficulty profile from current metrics.
        /// </summary>
        public void CalculateDifficultyProfile(int width, int height, int levels, Dictionary<string, string> parameters)
        {
            if (DifficultyProfile == null)
            {
                DifficultyProfile = new DifficultyProfile();
            }

            var components = new DifficultyComponents
            {
                Width = width,
                Height = height,
                Levels = levels,
                BranchingFactor = BranchingFactor,
                LoopRatio = LoopRatio,
                DeadEndCount = DeadEndCount,
                TotalRooms = Rooms,
                KeyLockChainDepth = ParseInt(parameters.GetValueOrDefault("keyLockChainDepth", "0")),
                SecretRoomDensity = ParseDouble(parameters.GetValueOrDefault("secretRoomDensity", "0")),
                PuzzleComplexity = ParseDouble(parameters.GetValueOrDefault("puzzleComplexity", "0")),
                EnemyCount = ParseInt(parameters.GetValueOrDefault("enemyCount", "0")),
                TrapDensity = ParseDouble(parameters.GetValueOrDefault("trapDensity", "0")),
                CombatDifficulty = ParseDouble(parameters.GetValueOrDefault("combatDifficulty", "0")),
                ResourceAvailability = ParseDouble(parameters.GetValueOrDefault("resourceAvailability", "0.5"))
            };

            DifficultyProfile.Components = components;
            DifficultyProfile.CalculateDifficultyScore();
            DifficultyProfile.PredictSuccessRate();
            PredictedAgentSuccessRate = DifficultyProfile.PredictedSuccessRate;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out var result) ? result : 0.0;
        }
    }
}



