using System;
using System.Collections.Generic;

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
        private readonly List<string> _validationFailures = new();

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

        public IReadOnlyDictionary<int, int> PathLengthHistogram => _pathLengthHistogram;
        public IReadOnlyDictionary<string, double> BiomeCoverage => _biomeCoverage;
        public IReadOnlyDictionary<string, double> PhaseDurationsMs => _phaseDurationsMs;
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
    }
}



