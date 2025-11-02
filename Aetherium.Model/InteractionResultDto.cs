using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// DTO for a single usage option returned from reactive disambiguation.
    /// </summary>
    public class UsageOptionDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class InteractionResultDto
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<UsageOptionDto>? Options { get; set; } // For reactive disambiguation
    }
}



