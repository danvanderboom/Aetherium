using System.Collections.Generic;

namespace WorldGenCLI.Models
{
    /// <summary>
    /// Response from generation endpoint.
    /// </summary>
    public sealed class GenerateResponse
    {
        public bool Success { get; set; }
        public MapRenderDto? Map { get; set; }
        public GenerationMetricsDto? Metrics { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public int Seed { get; set; }
    }

    public sealed class GenerationMetricsDto
    {
        public double BranchingFactor { get; set; }
        public double LoopRatio { get; set; }
        public int DeadEndCount { get; set; }
        public int Rooms { get; set; }
        public int Corridors { get; set; }
        public int SecretsPlaced { get; set; }
        public int TrapsPlaced { get; set; }
        public Dictionary<string, double>? BiomeCoverage { get; set; }
        public Dictionary<string, double>? PhaseDurationsMs { get; set; }
        public bool ValidationPassed { get; set; }
    }
}

