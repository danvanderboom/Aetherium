using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for teleporting to a specific location (admin/debug capability required).
    /// </summary>
    [AgentTool("jumptolocation", "Teleport to coordinates", 
        Categories = new[] { "movement", "navigation", "admin" },
        RequiredCapabilities = new[] { "admin" })]
    public class JumpToLocationTool : IAgentTool
    {
        public string ToolId => "jumptolocation";
        public string Description => "Teleport to specific coordinates (requires admin capability)";
        public IEnumerable<string> Categories => new[] { "movement", "navigation", "admin" };
        public IEnumerable<string> RequiredCapabilities => new[] { "admin" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["x"] = new()
                    {
                        Type = "number",
                        Description = "X coordinate"
                    },
                    ["y"] = new()
                    {
                        Type = "number",
                        Description = "Y coordinate"
                    },
                    ["z"] = new()
                    {
                        Type = "number",
                        Description = "Z coordinate (level)",
                        DefaultValue = 0
                    },
                    ["random"] = new()
                    {
                        Type = "boolean",
                        Description = "Jump to a random location (ignores x/y/z)",
                        DefaultValue = false
                    }
                },
                Required = new List<string>() // x/y required if not random
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("admin"))
                return ToolExecutionResult.Error("Missing required capability: admin");
            
            // Check for random jump
            bool random = false;
            if (args.TryGetValue("random", out var randomObj))
            {
                if (randomObj is bool boolRandom)
                    random = boolRandom;
                else if (bool.TryParse(randomObj.ToString(), out var parsed))
                    random = parsed;
            }
            
            if (context.Session != null)
            {
                if (random)
                {
                    context.Session.JumpToRandomLocation();
                    return ToolExecutionResult.Ok("Jumped to random location");
                }
                else
                {
                    // For non-random jumps, we need x and y coordinates
                    if (!args.TryGetValue("x", out var xObj) || !args.TryGetValue("y", out var yObj))
                        return ToolExecutionResult.Error("Missing required parameters: x and y (or use random=true)");
                    
                    int x, y, z = 0;
                    
                    if (xObj is int intX)
                        x = intX;
                    else if (!int.TryParse(xObj.ToString(), out x))
                        return ToolExecutionResult.Error("Invalid x coordinate");
                    
                    if (yObj is int intY)
                        y = intY;
                    else if (!int.TryParse(yObj.ToString(), out y))
                        return ToolExecutionResult.Error("Invalid y coordinate");
                    
                    if (args.TryGetValue("z", out var zObj))
                    {
                        if (zObj is int intZ)
                            z = intZ;
                        else if (!int.TryParse(zObj.ToString(), out z))
                            return ToolExecutionResult.Error("Invalid z coordinate");
                    }
                    
                    // Note: Direct coordinate jumping would require adding a method to GameSession
                    // For now, we only support random jumping
                    return ToolExecutionResult.Error("Direct coordinate jumping not yet implemented. Use random=true.");
                }
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

