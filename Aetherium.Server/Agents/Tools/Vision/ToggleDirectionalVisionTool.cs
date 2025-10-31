using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Vision
{
    /// <summary>
    /// Tool for toggling directional vision mode on/off.
    /// </summary>
    [AgentTool("toggledirectionalvision", "Toggle directional vision mode", 
        Categories = new[] { "vision", "perception" },
        RequiredCapabilities = new[] { "vision" })]
    public class ToggleDirectionalVisionTool : IAgentTool
    {
        public string ToolId => "toggledirectionalvision";
        public string Description => "Toggle directional vision mode on or off (forward-facing cone)";
        public IEnumerable<string> Categories => new[] { "vision", "perception" };
        public IEnumerable<string> RequiredCapabilities => new[] { "vision" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["enabled"] = new()
                    {
                        Type = "boolean",
                        Description = "True to enable directional vision, false to disable (if omitted, toggles current state)"
                    }
                },
                Required = new List<string>() // Optional parameter
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("vision"))
                return ToolExecutionResult.Error("Missing required capability: vision");
            
            // Use management grain if available
            if (context.ManagementGrain != null)
            {
                // Determine new state
                bool newState;
                if (args.TryGetValue("enabled", out var enabledObj))
                {
                    if (enabledObj is bool boolEnabled)
                        newState = boolEnabled;
                    else if (bool.TryParse(enabledObj.ToString(), out var parsed))
                        newState = parsed;
                    else
                        return ToolExecutionResult.Error("Invalid enabled value");
                }
                else
                {
                    // Toggle current state - need to get current state first
                    var visionStatus = await context.ManagementGrain.GetVisionStatusAsync(context.SessionId);
                    newState = visionStatus != null ? !visionStatus.DirectionalVisionMode : true;
                }
                
                var result = await context.ManagementGrain.SetDirectionalVisionAsync(context.SessionId, newState);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Directional vision {(newState ? "enabled" : "disabled")}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use session directly
            if (context.Session != null)
            {
                bool newState = context.Session.DirectionalVisionMode;
                
                if (args.TryGetValue("enabled", out var enabledObj))
                {
                    if (enabledObj is bool boolEnabled)
                        newState = boolEnabled;
                    else if (bool.TryParse(enabledObj.ToString(), out var parsed))
                        newState = parsed;
                    else
                        return ToolExecutionResult.Error("Invalid enabled value");
                }
                else
                {
                    // Toggle
                    newState = !newState;
                }
                
                context.Session.DirectionalVisionMode = newState;
                return ToolExecutionResult.Ok($"Directional vision {(newState ? "enabled" : "disabled")}");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

