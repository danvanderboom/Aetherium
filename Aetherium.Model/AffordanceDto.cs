using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// DTO for a single usage option within an affordance.
    /// </summary>
    public class AffordanceUsageDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? TargetId { get; set; }
    }

    public class AffordanceDto
    {
        public string Action { get; set; } = string.Empty; // pickup|drop|use|open|close
        public string ActorId { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? ItemId { get; set; } // Item entity ID for "use" actions
        public string? RequiresKeyId { get; set; }
        public List<AffordanceUsageDto> UsageOptions { get; set; } = new List<AffordanceUsageDto>();
    }
}



