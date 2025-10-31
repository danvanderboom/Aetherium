using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.Agents.Tools.Vision
{
    /// <summary>
    /// Tool for setting the lighting mode.
    /// </summary>
    [AgentTool("setlightingmode", "Set the lighting mode", 
        Categories = new[] { "vision", "perception" },
        RequiredCapabilities = new[] { "vision" })]
    public class SetLightingModeTool : IAgentTool
    {
        public string ToolId => "setlightingmode";
        public string Description => "Set the lighting mode (Torch, Sunlight, Darkness)";
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
                        Description = "Lighting mode to set",
                        AllowedValues = new() { "Torch", "Sunlight", "Darkness" }
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
            
            if (!System.Enum.TryParse<LightingMode>(modeStr, true, out var mode))
                return ToolExecutionResult.Error($"Invalid lighting mode: {modeStr}. Must be Torch, Sunlight, or Darkness");
            
            // Use management grain if available
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.SetLightingModeAsync(context.SessionId, mode);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Set lighting mode to {mode}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use session directly
            if (context.Session != null)
            {
                context.Session.CurrentLightingMode = mode;
                return ToolExecutionResult.Ok($"Set lighting mode to {mode}");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

