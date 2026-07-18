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
            
            // Route through the gateway FIRST. For a grain-bound session this is the
            // GrainMutationGateway — the ONLY path that moves the canonical player. The
            // management-grain path below calls session.MoveView (session-local): checking
            // it first silently left the canonical body parked at its spawn while the
            // client walked a phantom — monsters then converged on and killed a body the
            // player couldn't see ("continuous damage with no monster in sight").
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
                        // Absolute directions translate against the actor's current heading into
                        // the equivalent single relative step (0°→F, 90°→R, 180°→B, 270°→L) —
                        // same net movement as the management path's rotate/move/rotate-back,
                        // without changing heading, and it stays on the gateway so a grain-bound
                        // session mutates CANONICAL state. Falls back to the management grain
                        // when no session heading is available.
                        if (context.Session != null)
                        {
                            int target = direction[0] switch { 'E' => 90, 'S' => 180, 'W' => 270, _ => 0 };
                            int diff = ((target - context.Session.HeadingDegrees) % 360 + 360) % 360;
                            relDir = diff switch
                            {
                                90 => Aetherium.Model.RelativeDirection.Right,
                                180 => Aetherium.Model.RelativeDirection.Backward,
                                270 => Aetherium.Model.RelativeDirection.Left,
                                _ => Aetherium.Model.RelativeDirection.Forward,
                            };
                            break;
                        }
                        if (context.ManagementGrain != null)
                        {
                            var absolute = await context.ManagementGrain.MoveAsync(context.SessionId, direction);
                            return absolute.Success
                                ? ToolExecutionResult.Ok($"Moved {direction}")
                                : ToolExecutionResult.Error(absolute.Message);
                        }
                        return ToolExecutionResult.Error("Absolute directions require async execution via management grain");
                    default:
                        return ToolExecutionResult.Error($"Invalid direction: {direction}");
                }

                var moveResult = await context.MutationGateway.MoveAsync(relDir, distance);
                return moveResult.Success
                    ? ToolExecutionResult.Ok($"Moved {direction} by {distance}")
                    : ToolExecutionResult.Error(moveResult.Reason ?? "Move failed");
            }

            // Fallback: management-grain dispatch — agent runners that operate through
            // IGameManagementGrain without holding a session/gateway reference.
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.MoveAsync(context.SessionId, direction);
                return result.Success
                    ? ToolExecutionResult.Ok($"Moved {direction}")
                    : ToolExecutionResult.Error(result.Message);
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

