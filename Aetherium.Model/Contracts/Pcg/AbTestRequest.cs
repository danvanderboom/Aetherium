using System.Collections.Generic;
using WorldGenCLI.Models;

namespace WorldGenCLI.Models
{
    /// <summary>
    /// Request for A/B testing multiple candidates.
    /// </summary>
    public sealed class AbTestRequest
    {
        public string GeneratorId { get; set; } = string.Empty;
        public GenerateRequest BaseRequest { get; set; } = null!;
        public int Count { get; set; } = 5;
        public string? TopByMetric { get; set; } // e.g., "branchingFactor", "loopRatio"
        public int Limit { get; set; } = 10;
        public List<int>? Seeds { get; set; } // Optional explicit seeds
    }
}

