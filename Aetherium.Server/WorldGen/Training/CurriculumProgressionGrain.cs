using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Server.Agents.Telemetry;
using Orleans;

namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Orleans grain implementation for tracking agent progression through curricula.
    /// </summary>
    public class CurriculumProgressionGrain : Grain, ICurriculumProgressionGrain
    {
        private string? _curriculumId;
        private string? _agentId;
        private CurriculumDefinition? _curriculum;
        private string? _currentStageId;
        private readonly Dictionary<string, StageProgress> _stageProgress = new Dictionary<string, StageProgress>();
        private IAgentTelemetryGrain? _telemetryGrain;

        public override Task OnActivateAsync(System.Threading.CancellationToken cancellationToken)
        {
            var key = this.GetPrimaryKeyString();
            Console.WriteLine($"[CurriculumProgression] Activated for agent: {key}");
            return base.OnActivateAsync(cancellationToken);
        }

        public Task StartCurriculumAsync(string curriculumId, string agentId)
        {
            _curriculumId = curriculumId;
            _agentId = agentId;
            
            // Load curriculum (in production, would load from storage)
            // For now, we'll use a simple lookup - this would be replaced with actual storage
            _curriculum = CurriculumLibrary.GetCurriculum(curriculumId);
            
            if (_curriculum == null)
            {
                Console.WriteLine($"[CurriculumProgression] Curriculum not found: {curriculumId}");
                return Task.CompletedTask;
            }

            // Get telemetry grain for analysis
            if (_agentId != null)
            {
                _telemetryGrain = GrainFactory.GetGrain<IAgentTelemetryGrain>(_agentId);
            }

            // Start at first stage
            if (_curriculum.Stages.Count > 0)
            {
                _currentStageId = _curriculum.Stages[0].StageId;
            }

            return Task.CompletedTask;
        }

        public Task<CurriculumStage?> GetCurrentStageAsync()
        {
            if (_curriculum == null || _currentStageId == null)
                return Task.FromResult<CurriculumStage?>(null);

            var stage = _curriculum.GetStage(_currentStageId);
            return Task.FromResult(stage);
        }

        public async Task RecordRunAsync(bool successful, int steps, Dictionary<string, object>? metadata = null)
        {
            if (_currentStageId == null || _curriculum == null)
                return;

            if (!_stageProgress.TryGetValue(_currentStageId, out var progress))
            {
                progress = new StageProgress
                {
                    StageId = _currentStageId
                };
                _stageProgress[_currentStageId] = progress;
            }

            progress.TotalRuns++;
            if (successful)
            {
                progress.SuccessfulRuns++;
            }
            progress.TotalSteps += steps;
            progress.Metadata = metadata ?? new Dictionary<string, object>();

            // Auto-progress if enabled
            if (_curriculum.AutoProgression)
            {
                await TryAdvanceStageAsync();
            }
        }

        public async Task<CurriculumProgress> GetProgressAsync()
        {
            var progress = new CurriculumProgress
            {
                CurriculumId = _curriculumId ?? string.Empty,
                CurrentStageId = _currentStageId ?? string.Empty,
                TotalStages = _curriculum?.Stages.Count ?? 0,
                StageProgress = new Dictionary<string, object>()
            };

            // Count completed stages
            foreach (var stage in _curriculum?.Stages ?? new List<CurriculumStage>())
            {
                if (_stageProgress.TryGetValue(stage.StageId, out var stageProgress))
                {
                    var criteria = stage.CompletionCriteria;
                    if (stageProgress.TotalRuns >= criteria.MinAttempts &&
                        stageProgress.SuccessfulRuns >= criteria.MinSuccessfulCompletions)
                    {
                        progress.CompletedStages++;
                    }

                    progress.StageProgress[stage.StageId] = new
                    {
                        totalRuns = stageProgress.TotalRuns,
                        successfulRuns = stageProgress.SuccessfulRuns,
                        successRate = stageProgress.TotalRuns > 0 
                            ? (double)stageProgress.SuccessfulRuns / stageProgress.TotalRuns 
                            : 0.0
                    };
                }
            }

            // Calculate overall stats
            progress.TotalRuns = _stageProgress.Values.Sum(p => p.TotalRuns);
            progress.SuccessfulRuns = _stageProgress.Values.Sum(p => p.SuccessfulRuns);
            progress.CurrentSuccessRate = progress.TotalRuns > 0
                ? (double)progress.SuccessfulRuns / progress.TotalRuns
                : 0.0;

            return progress;
        }

        public async Task<bool> TryAdvanceStageAsync()
        {
            if (_curriculum == null || _currentStageId == null)
                return false;

            var currentStage = _curriculum.GetStage(_currentStageId);
            if (currentStage == null)
                return false;

            // Check if current stage is complete
            var progress = _stageProgress.TryGetValue(_currentStageId, out var p) ? p : null;
            if (progress == null)
                return false;

            PerformanceAnalysis? analysis = null;
            if (_telemetryGrain != null)
            {
                analysis = await _telemetryGrain.GetAnalysisAsync();
            }

            bool canAdvance = AutoCurriculumGenerator.IsReadyForNextStage(
                currentStage,
                analysis ?? new PerformanceAnalysis { SuccessRate = progress.TotalRuns > 0 ? (double)progress.SuccessfulRuns / progress.TotalRuns : 0 },
                progress.SuccessfulRuns,
                progress.TotalRuns);

            if (!canAdvance)
                return false;

            // Find next stage
            var currentIndex = _curriculum.Stages.FindIndex(s => s.StageId == _currentStageId);
            if (currentIndex >= 0 && currentIndex < _curriculum.Stages.Count - 1)
            {
                var nextStage = _curriculum.Stages[currentIndex + 1];
                
                // Check prerequisites
                var prerequisitesMet = true;
                foreach (var requiredStageId in nextStage.Prerequisites.RequiredStageIds)
                {
                    if (!_stageProgress.TryGetValue(requiredStageId, out var reqProgress) ||
                        reqProgress.TotalRuns < nextStage.Prerequisites.MinCompletedRuns)
                    {
                        prerequisitesMet = false;
                        break;
                    }
                }

                if (prerequisitesMet)
                {
                    _currentStageId = nextStage.StageId;
                    return true;
                }
            }

            return false;
        }

        public async Task<CurriculumStage?> GetNextTrainingStageAsync()
        {
            if (_curriculum == null)
                return null;

            // If auto-progression and current stage is complete, try to advance
            if (_curriculum.AutoProgression)
            {
                await TryAdvanceStageAsync();
            }

            var currentStage = await GetCurrentStageAsync();
            if (currentStage != null)
                return currentStage;

            // If no current stage or auto-progression, generate next stage
            if (_telemetryGrain != null && _agentId != null)
            {
                var analysis = await _telemetryGrain.GetAnalysisAsync();
                if (analysis != null)
                {
                    var nextStage = AutoCurriculumGenerator.GenerateNextStage(
                        analysis,
                        currentStage,
                        1); // TODO: track actual skill level
                    return nextStage;
                }
            }

            // Fallback to first stage
            if (_curriculum.Stages.Count > 0)
            {
                return _curriculum.Stages[0];
            }

            return null;
        }

        public Task ResetAsync()
        {
            _curriculumId = null;
            _agentId = null;
            _curriculum = null;
            _currentStageId = null;
            _stageProgress.Clear();
            return Task.CompletedTask;
        }

        private sealed class StageProgress
        {
            public string StageId { get; set; } = string.Empty;
            public int TotalRuns { get; set; }
            public int SuccessfulRuns { get; set; }
            public int TotalSteps { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Simple in-memory curriculum library. In production, would load from storage.
    /// </summary>
    internal static class CurriculumLibrary
    {
        private static readonly Dictionary<string, CurriculumDefinition> _curricula = new Dictionary<string, CurriculumDefinition>();

        static CurriculumLibrary()
        {
            // Load curricula from Data/Curricula on initialization
            // For now, just provides lookup
        }

        public static CurriculumDefinition? GetCurriculum(string curriculumId)
        {
            return _curricula.TryGetValue(curriculumId, out var curriculum) ? curriculum : null;
        }

        public static void RegisterCurriculum(CurriculumDefinition curriculum)
        {
            _curricula[curriculum.CurriculumId] = curriculum;
        }
    }
}

