using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Vision
{
    /// <summary>
    /// Tool for setting the field of view in degrees.
    /// </summary>
    [AgentTool("setfieldofview", "Set field of view in degrees", 
        Categories = new[] { "vision", "perception" },
        RequiredCapabilities = new[] { "vision" })]
    public class SetFieldOfViewTool : IAgentTool
    {
        public string ToolId => "setfieldofview";
        public string Description => "Set the field of view (FOV) in degrees (1-360)";
        public IEnumerable<string> Categories => new[] { "vision", "perception" };
        public IEnumerable<string> RequiredCapabilities => new[] { "vision" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["degrees"] = new()
                    {
                        Type = "number",
                        Description = "Field of view in degrees (1-360)"
                    }
                },
                Required = new() { "degrees" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("vision"))
                return ToolExecutionResult.Error("Missing required capability: vision");
            
            if (!args.TryGetValue("degrees", out var degObj))
                return ToolExecutionResult.Error("Missing required parameter: degrees");
            
            int degrees;
            if (degObj is int intDeg)
                degrees = intDeg;
            else if (int.TryParse(degObj.ToString(), out var parsed))
                degrees = parsed;
            else
                return ToolExecutionResult.Error("Invalid degrees value");
            
            if (degrees < 1 || degrees > 360)
                return ToolExecutionResult.Error("Degrees must be between 1 and 360");
            
            // Use management grain if available
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.SetFieldOfViewAsync(context.SessionId, degrees);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Set FOV to {degrees}°")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use session directly
            if (context.Session?.Player != null)
            {
                var hasHeading = context.Session.Player.Get<Aetherium.Components.HasHeading>();
                if (hasHeading != null)
                {
                    hasHeading.FieldOfViewDegrees = degrees;
                    return ToolExecutionResult.Ok($"Set FOV to {degrees}°");
                }
                return ToolExecutionResult.Error("Player does not have heading component");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

