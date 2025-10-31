using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for changing the agent's Z-level (moving up or down floors).
    /// </summary>
    [AgentTool("changelevel", "Move up or down Z-levels", 
        Categories = new[] { "movement", "navigation" },
        RequiredCapabilities = new[] { "basic_movement" })]
    public class ChangeLevelTool : IAgentTool
    {
        public string ToolId => "changelevel";
        public string Description => "Move up or down Z-levels (floors)";
        public IEnumerable<string> Categories => new[] { "movement", "navigation" };
        public IEnumerable<string> RequiredCapabilities => new[] { "basic_movement" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["delta"] = new()
                    {
                        Type = "number",
                        Description = "Number of levels to move (positive = up, negative = down)"
                    }
                },
                Required = new() { "delta" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("basic_movement"))
                return ToolExecutionResult.Error("Missing required capability: basic_movement");
            
            if (!args.TryGetValue("delta", out var deltaObj))
                return ToolExecutionResult.Error("Missing required parameter: delta");
            
            int delta;
            if (deltaObj is int intDelta)
                delta = intDelta;
            else if (int.TryParse(deltaObj.ToString(), out var parsed))
                delta = parsed;
            else
                return ToolExecutionResult.Error("Invalid delta value");
            
            if (delta < -100 || delta > 100)
                return ToolExecutionResult.Error("Delta must be between -100 and 100");
            
            // Use session directly
            if (context.Session != null)
            {
                context.Session.ChangeLevel(delta);
                return ToolExecutionResult.Ok($"Changed level by {delta}");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

