using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// DTO for a single usage option returned from reactive disambiguation.
    /// </summary>
    [GenerateSerializer]
    public class UsageOptionDto
    {
        [Id(0)] public string UsageId { get; set; } = string.Empty;
        [Id(1)] public string Label { get; set; } = string.Empty;
        [Id(2)] public string Description { get; set; } = string.Empty;
    }

    [GenerateSerializer]
    public class InteractionResultDto
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string Reason { get; set; } = string.Empty;
        [Id(2)] public List<UsageOptionDto>? Options { get; set; } // For reactive disambiguation
    }
}



