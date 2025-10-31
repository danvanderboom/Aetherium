using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Defines the parameter schema for a tool.
    /// Can be converted to OpenAI function calling format or simple text format.
    /// </summary>
    public class ToolParameterSchema
    {
        /// <summary>
        /// Parameter definitions by name.
        /// </summary>
        public Dictionary<string, ParameterDefinition> Properties { get; set; } = new();
        
        /// <summary>
        /// Names of required parameters.
        /// </summary>
        public List<string> Required { get; set; } = new();
        
        /// <summary>
        /// Converts the schema to OpenAI function calling format.
        /// </summary>
        public object ToOpenAIFormat()
        {
            var properties = new Dictionary<string, object>();
            
            foreach (var kvp in Properties)
            {
                var prop = new Dictionary<string, object>
                {
                    ["type"] = kvp.Value.Type,
                    ["description"] = kvp.Value.Description
                };
                
                if (kvp.Value.AllowedValues.Count > 0)
                {
                    prop["enum"] = kvp.Value.AllowedValues;
                }
                
                if (kvp.Value.DefaultValue != null)
                {
                    prop["default"] = kvp.Value.DefaultValue;
                }
                
                properties[kvp.Key] = prop;
            }
            
            return new
            {
                type = "object",
                properties = properties,
                required = Required
            };
        }
        
        /// <summary>
        /// Converts the schema to a simple text format for prompt injection.
        /// </summary>
        public string ToSimpleFormat()
        {
            if (Properties.Count == 0)
                return "no parameters";
            
            var sb = new StringBuilder();
            var isFirst = true;
            
            foreach (var kvp in Properties)
            {
                if (!isFirst) sb.Append(", ");
                isFirst = false;
                
                sb.Append(kvp.Key);
                sb.Append(": ");
                sb.Append(kvp.Value.Type);
                
                if (Required.Contains(kvp.Key))
                    sb.Append(" (required)");
                
                if (kvp.Value.AllowedValues.Count > 0)
                {
                    sb.Append(" [");
                    sb.Append(string.Join("|", kvp.Value.AllowedValues));
                    sb.Append("]");
                }
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Defines a single parameter for a tool.
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// Parameter type (string, number, boolean, object, array).
        /// </summary>
        public string Type { get; set; } = "string";
        
        /// <summary>
        /// Human-readable description of the parameter.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Allowed values (enum).
        /// </summary>
        public List<string> AllowedValues { get; set; } = new();
        
        /// <summary>
        /// Default value if not provided.
        /// </summary>
        public object? DefaultValue { get; set; }
    }
}

