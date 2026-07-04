using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Aetherium.Model.Training
{
    /// <summary>
    /// Orleans grain for tracking agent progression through curriculum stages.
    /// </summary>
    public interface ICurriculumProgressionGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Starts or resumes a curriculum for an agent.
        /// </summary>
        Task StartCurriculumAsync(string curriculumId, string agentId);

        /// <summary>
        /// Gets the current stage the agent is working on.
        /// </summary>
        Task<CurriculumStage?> GetCurrentStageAsync();

        /// <summary>
        /// Records a training run completion.
        /// </summary>
        Task RecordRunAsync(bool successful, int steps, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// Gets progress information for the current curriculum.
        /// </summary>
        Task<CurriculumProgress> GetProgressAsync();

        /// <summary>
        /// Advances to the next stage if prerequisites are met.
        /// </summary>
        Task<bool> TryAdvanceStageAsync();

        /// <summary>
        /// Gets the next stage to use for training (auto-generated if in auto mode).
        /// </summary>
        Task<CurriculumStage?> GetNextTrainingStageAsync();

        /// <summary>
        /// Clears progression data (resets curriculum).
        /// </summary>
        Task ResetAsync();
    }

    /// <summary>
    /// Progress information for an agent's curriculum.
    /// </summary>
    [Orleans.GenerateSerializer]
    public sealed class CurriculumProgress
    {
        [Orleans.Id(0)]
        public string CurriculumId { get; set; } = string.Empty;

        [Orleans.Id(1)]
        public string CurrentStageId { get; set; } = string.Empty;

        [Orleans.Id(2)]
        public int TotalStages { get; set; }

        [Orleans.Id(3)]
        public int CompletedStages { get; set; }

        [Orleans.Id(4)]
        public int TotalRuns { get; set; }

        [Orleans.Id(5)]
        public int SuccessfulRuns { get; set; }

        [Orleans.Id(6)]
        public double CurrentSuccessRate { get; set; }

        [Orleans.Id(7)]
        public Dictionary<string, CurriculumStageProgressInfo> StageProgress { get; set; } = new Dictionary<string, CurriculumStageProgressInfo>();
    }

    [Orleans.GenerateSerializer]
    public sealed class CurriculumStageProgressInfo
    {
        [Orleans.Id(0)] public int TotalRuns { get; set; }
        [Orleans.Id(1)] public int SuccessfulRuns { get; set; }
        [Orleans.Id(2)] public double SuccessRate { get; set; }
    }
}

