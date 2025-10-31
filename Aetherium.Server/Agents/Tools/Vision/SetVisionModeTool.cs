using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.Agents.Tools.Vision
{
    /// <summary>
    /// Tool for setting the vision mode.
    /// </summary>
    [AgentTool("setvisionmode", "Set the vision mode", 
        Categories = new[] { "vision", "perception" },
        RequiredCapabilities = new[] { "vision" })]
    public class SetVisionModeTool : IAgentTool
    {
        public string ToolId => "setvisionmode";
        public string Description => "Set the vision mode (Normal, Infrared, UltraViolet, Sonar)";
        public IEnumerable<string> Categories => new[] { "vision", "perception" };
        public IEnumerable<string> RequiredCapabilities => new[] { "vision" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["mode"] = new()
                    {
                        Type = "string",
                        Description = "Vision mode to set",
                        AllowedValues = new() { "Normal", "Infrared", "UltraViolet", "Sonar" }
                    }
                },
                Required = new() { "mode" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("vision"))
                return ToolExecutionResult.Error("Missing required capability: vision");
            
            if (!args.TryGetValue("mode", out var modeObj))
                return ToolExecutionResult.Error("Missing required parameter: mode");
            
            var modeStr = modeObj.ToString()?.Trim();
            if (string.IsNullOrEmpty(modeStr))
                return ToolExecutionResult.Error("Mode cannot be empty");
            
            if (!System.Enum.TryParse<VisionMode>(modeStr, true, out var mode))
                return ToolExecutionResult.Error($"Invalid vision mode: {modeStr}. Must be Normal, Infrared, UltraViolet, or Sonar");
            
            // Use management grain if available
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.SetVisionModeAsync(context.SessionId, mode);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Set vision mode to {mode}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use session directly
            if (context.Session != null)
            {
                context.Session.CurrentVisionMode = mode;
                return ToolExecutionResult.Ok($"Set vision mode to {mode}");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

