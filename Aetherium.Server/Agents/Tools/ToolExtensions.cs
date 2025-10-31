using System.Linq;
using Aetherium.Model;

namespace Aetherium.Server.Agents.Tools
{
    /// <summary>
    /// Extension methods for converting between tool types and DTOs.
    /// </summary>
    public static class ToolExtensions
    {
        /// <summary>
        /// Converts a ToolExecutionResult to its DTO representation.
        /// </summary>
        public static ToolExecutionResultDto ToDto(this ToolExecutionResult result)
        {
            return new ToolExecutionResultDto
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data
            };
        }
        
        /// <summary>
        /// Converts an IAgentTool to its DTO representation.
        /// </summary>
        public static ToolInfoDto ToDto(this IAgentTool tool)
        {
            return new ToolInfoDto
            {
                ToolId = tool.ToolId,
                Description = tool.Description,
                Categories = tool.Categories.ToArray(),
                RequiredCapabilities = tool.RequiredCapabilities.ToArray(),
                ParameterSchema = tool.GetParameterSchema().ToDto()
            };
        }
        
        /// <summary>
        /// Converts a ToolParameterSchema to its DTO representation.
        /// </summary>
        public static ToolParameterSchemaDto ToDto(this ToolParameterSchema schema)
        {
            return new ToolParameterSchemaDto
            {
                Properties = schema.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDto()
                ),
                Required = schema.Required
            };
        }
        
        /// <summary>
        /// Converts a ParameterDefinition to its DTO representation.
        /// </summary>
        public static ParameterDefinitionDto ToDto(this ParameterDefinition def)
        {
            return new ParameterDefinitionDto
            {
                Type = def.Type,
                Description = def.Description,
                AllowedValues = def.AllowedValues,
                DefaultValue = def.DefaultValue
            };
        }
    }
}

