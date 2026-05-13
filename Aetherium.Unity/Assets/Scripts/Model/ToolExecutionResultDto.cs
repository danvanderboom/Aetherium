#nullable enable
using System.Collections.Generic;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// DTO for tool execution results.
    /// </summary>
    public class ToolExecutionResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Data { get; set; }
    }
    
    /// <summary>
    /// DTO for a single usage option returned from reactive disambiguation.
    /// </summary>
    public class UsageOptionDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

