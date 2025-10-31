using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for moving the agent in a specified direction.
    /// Supports both relative (F/L/R/B) and absolute (N/E/S/W) directions.
    /// </summary>
    [AgentTool("move", "Move in a specified direction", 
        Categories = new[] { "movement", "navigation" },
        RequiredCapabilities = new[] { "basic_movement" })]
    public class MoveTool : IAgentTool
    {
        public string ToolId => "move";
        public string Description => "Move in a specified direction (F/L/R/B for relative, N/E/S/W for absolute)";
        public IEnumerable<string> Categories => new[] { "movement", "navigation" };
        public IEnumerable<string> RequiredCapabilities => new[] { "basic_movement" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["direction"] = new()
                    {
                        Type = "string",
                        Description = "Direction to move: F/FORWARD, B/BACKWARD, L/LEFT, R/RIGHT, N/NORTH, E/EAST, S/SOUTH, W/WEST",
                        AllowedValues = new() { "F", "FORWARD", "B", "BACKWARD", "L", "LEFT", "R", "RIGHT", "N", "NORTH", "E", "EAST", "S", "SOUTH", "W", "WEST" }
                    },
                    ["distance"] = new()
                    {
                        Type = "number",
                        Description = "Distance to move (default: 1)",
                        DefaultValue = 1
                    }
                },
                Required = new() { "direction" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("basic_movement"))
                return ToolExecutionResult.Error("Missing required capability: basic_movement");
            
            if (!args.TryGetValue("direction", out var dirObj))
                return ToolExecutionResult.Error("Missing required parameter: direction");
            
            var direction = dirObj.ToString()?.Trim().ToUpperInvariant() ?? "F";
            var distance = 1;
            
            if (args.TryGetValue("distance", out var distObj))
            {
                if (distObj is int intDist)
                    distance = intDist;
                else if (int.TryParse(distObj.ToString(), out var parsed))
                    distance = parsed;
            }
            
            if (distance < 1 || distance > 100)
                return ToolExecutionResult.Error("Distance must be between 1 and 100");
            
            // Use management grain if available (for agent execution)
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.MoveAsync(context.SessionId, direction);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Moved {direction}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Otherwise use session directly (for synchronous player execution)
            if (context.Session != null)
            {
                // Parse direction to RelativeDirection
                Aetherium.Model.RelativeDirection relDir;
                switch (direction)
                {
                    case "F" or "FORWARD":
                        relDir = Aetherium.Model.RelativeDirection.Forward;
                        break;
                    case "B" or "BACKWARD":
                        relDir = Aetherium.Model.RelativeDirection.Backward;
                        break;
                    case "L" or "LEFT":
                        relDir = Aetherium.Model.RelativeDirection.Left;
                        break;
                    case "R" or "RIGHT":
                        relDir = Aetherium.Model.RelativeDirection.Right;
                        break;
                    case "N" or "NORTH":
                    case "E" or "EAST":
                    case "S" or "SOUTH":
                    case "W" or "WEST":
                        // For absolute directions, we'll need to calculate relative direction
                        // This is handled by the management grain in the async path
                        return ToolExecutionResult.Error("Absolute directions require async execution via management grain");
                    default:
                        return ToolExecutionResult.Error($"Invalid direction: {direction}");
                }
                
                context.Session.MoveView(relDir, distance);
                return ToolExecutionResult.Ok($"Moved {direction} by {distance}");
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

