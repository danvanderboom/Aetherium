using System;
using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// DTO for tool execution results.
    /// </summary>
    [GenerateSerializer]
    public class ToolExecutionResultDto
    {
        [Id(0)]
        public bool Success { get; set; }
        [Id(1)]
        public string Message { get; set; } = string.Empty;
        [Id(2)]
        public Dictionary<string, object>? Data { get; set; }
    }
    
    /// <summary>
    /// DTO for a single usage option of a multi-use tool.
    /// </summary>
    [GenerateSerializer]
    public class ToolUsageOptionDto
    {
        [Id(0)]
        public string UsageId { get; set; } = string.Empty;
        [Id(1)]
        public string Label { get; set; } = string.Empty;
        [Id(2)]
        public string Description { get; set; } = string.Empty;
        [Id(3)]
        public ToolParameterSchemaDto? ParameterSchemaOverride { get; set; }
        [Id(4)]
        public string[] ContextRequirements { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// DTO for tool information.
    /// </summary>
    [GenerateSerializer]
    public class ToolInfoDto
    {
        [Id(0)]
        public string ToolId { get; set; } = string.Empty;
        [Id(1)]
        public string Description { get; set; } = string.Empty;
        [Id(2)]
        public string[] Categories { get; set; } = Array.Empty<string>();
        [Id(3)]
        public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
        [Id(4)]
        public ToolParameterSchemaDto ParameterSchema { get; set; } = new();
        [Id(5)]
        public bool IsMultiUse { get; set; } = false;
        [Id(6)]
        public ToolUsageOptionDto[] UsageOptions { get; set; } = Array.Empty<ToolUsageOptionDto>();
    }
    
    /// <summary>
    /// DTO for tool parameter schema.
    /// </summary>
    [GenerateSerializer]
    public class ToolParameterSchemaDto
    {
        [Id(0)]
        public Dictionary<string, ParameterDefinitionDto> Properties { get; set; } = new();
        [Id(1)]
        public List<string> Required { get; set; } = new();
    }
    
    /// <summary>
    /// DTO for parameter definitions.
    /// </summary>
    [GenerateSerializer]
    public class ParameterDefinitionDto
    {
        [Id(0)]
        public string Type { get; set; } = "string";
        [Id(1)]
        public string Description { get; set; } = string.Empty;
        [Id(2)]
        public List<string> AllowedValues { get; set; } = new();
        [Id(3)]
        public object? DefaultValue { get; set; }
    }
}

