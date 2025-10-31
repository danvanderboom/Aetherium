namespace Aetherium.Model
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
    /// DTO for tool information.
    /// </summary>
    public class ToolInfoDto
    {
        public string ToolId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Categories { get; set; } = Array.Empty<string>();
        public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
        public ToolParameterSchemaDto ParameterSchema { get; set; } = new();
    }
    
    /// <summary>
    /// DTO for tool parameter schema.
    /// </summary>
    public class ToolParameterSchemaDto
    {
        public Dictionary<string, ParameterDefinitionDto> Properties { get; set; } = new();
        public List<string> Required { get; set; } = new();
    }
    
    /// <summary>
    /// DTO for parameter definitions.
    /// </summary>
    public class ParameterDefinitionDto
    {
        public string Type { get; set; } = "string";
        public string Description { get; set; } = string.Empty;
        public List<string> AllowedValues { get; set; } = new();
        public object? DefaultValue { get; set; }
    }
}

