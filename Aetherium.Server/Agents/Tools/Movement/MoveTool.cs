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
            
            // Async path: management-grain dispatch (used by agent runners that operate
            // through IGameManagementGrain rather than holding a session reference).
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.MoveAsync(context.SessionId, direction);
                return result.Success
                    ? ToolExecutionResult.Ok($"Moved {direction}")
                    : ToolExecutionResult.Error(result.Message);
            }

            // In-process path: route through the gateway. Phase 2a's LocalMutationGateway
            // wraps GameSession.MoveView; phase 2b+c will swap this for a grain-routed
            // gateway transparently — this tool code does not change.
            if (context.MutationGateway != null)
            {
                Aetherium.Model.RelativeDirection relDir;
                switch (direction)
                {
                    case "F" or "FORWARD": relDir = Aetherium.Model.RelativeDirection.Forward; break;
                    case "B" or "BACKWARD": relDir = Aetherium.Model.RelativeDirection.Backward; break;
                    case "L" or "LEFT": relDir = Aetherium.Model.RelativeDirection.Left; break;
                    case "R" or "RIGHT": relDir = Aetherium.Model.RelativeDirection.Right; break;
                    case "N" or "NORTH":
                    case "E" or "EAST":
                    case "S" or "SOUTH":
                    case "W" or "WEST":
                        // Absolute directions require translation against the actor's heading;
                        // that's the management-grain path's job today.
                        return ToolExecutionResult.Error("Absolute directions require async execution via management grain");
                    default:
                        return ToolExecutionResult.Error($"Invalid direction: {direction}");
                }

                var moveResult = await context.MutationGateway.MoveAsync(relDir, distance);
                return moveResult.Success
                    ? ToolExecutionResult.Ok($"Moved {direction} by {distance}")
                    : ToolExecutionResult.Error(moveResult.Reason ?? "Move failed");
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

