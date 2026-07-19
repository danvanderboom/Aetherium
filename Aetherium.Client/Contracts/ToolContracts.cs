using System;
using System.Collections.Generic;

namespace Aetherium.Client.Contracts
{
    /// <summary>Result of ExecuteTool — every player action returns this shape.</summary>
    public class ToolExecutionResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Data { get; set; }
    }

    public class ToolUsageOptionDto
    {
        public string UsageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ToolParameterSchemaDto? ParameterSchemaOverride { get; set; }
        public string[] ContextRequirements { get; set; } = Array.Empty<string>();
    }

    /// <summary>Runtime tool schema from ListAvailableTools — used by the live drift check
    /// (dev builds warn when the server's schema disagrees with the typed wrappers).</summary>
    public class ToolInfoDto
    {
        public string ToolId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Categories { get; set; } = Array.Empty<string>();
        public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
        public ToolParameterSchemaDto ParameterSchema { get; set; } = new ToolParameterSchemaDto();
        public bool IsMultiUse { get; set; }
        public ToolUsageOptionDto[] UsageOptions { get; set; } = Array.Empty<ToolUsageOptionDto>();
    }

    public class ToolParameterSchemaDto
    {
        public Dictionary<string, ParameterDefinitionDto> Properties { get; set; } = new Dictionary<string, ParameterDefinitionDto>();
        public List<string> Required { get; set; } = new List<string>();
    }

    public class ParameterDefinitionDto
    {
        public string Type { get; set; } = "string";
        public string Description { get; set; } = string.Empty;
        public List<string> AllowedValues { get; set; } = new List<string>();
        public object? DefaultValue { get; set; }
    }
}
