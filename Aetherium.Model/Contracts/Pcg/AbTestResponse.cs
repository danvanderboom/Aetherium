using System.Collections.Generic;

namespace Aetherium.Model.Pcg
{
    /// <summary>
    /// Response from A/B testing endpoint.
    /// </summary>
    public sealed class AbTestResponse
    {
        public List<CandidateResult> Candidates { get; set; } = new List<CandidateResult>();
        public string? SortMetric { get; set; }
    }

    public sealed class CandidateResult
    {
        public int Seed { get; set; }
        public MapRenderDto? Map { get; set; }
        public GenerationMetricsDto? Metrics { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool Success { get; set; }
    }
}

