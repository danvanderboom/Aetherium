using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Movement
{
    /// <summary>
    /// Tool for rotating the agent's view.
    /// Supports both degree-based and clockwise/counter-clockwise rotation.
    /// </summary>
    [AgentTool("rotate", "Rotate the agent's view", 
        Categories = new[] { "movement", "navigation" },
        RequiredCapabilities = new[] { "basic_movement" })]
    public class RotateTool : IAgentTool
    {
        public string ToolId => "rotate";
        public string Description => "Rotate view by degrees or clockwise/counter-clockwise";
        public IEnumerable<string> Categories => new[] { "movement", "navigation" };
        public IEnumerable<string> RequiredCapabilities => new[] { "basic_movement" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["degrees"] = new()
                    {
                        Type = "number",
                        Description = "Degrees to rotate (positive = clockwise, negative = counter-clockwise). Mutually exclusive with 'clockwise'."
                    },
                    ["clockwise"] = new()
                    {
                        Type = "boolean",
                        Description = "True to rotate clockwise, false for counter-clockwise. Mutually exclusive with 'degrees'."
                    }
                },
                Required = new List<string>() // At least one is required, validated in Execute
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("basic_movement"))
                return ToolExecutionResult.Error("Missing required capability: basic_movement");
            
            int degrees = 0;
            bool hasDegrees = false;
            bool hasClockwise = false;
            
            if (args.TryGetValue("degrees", out var degObj))
            {
                if (degObj is int intDeg)
                    degrees = intDeg;
                else if (int.TryParse(degObj.ToString(), out var parsed))
                    degrees = parsed;
                else
                    return ToolExecutionResult.Error("Invalid degrees value");
                
                hasDegrees = true;
            }
            
            if (args.TryGetValue("clockwise", out var cwObj))
            {
                if (hasDegrees)
                    return ToolExecutionResult.Error("Cannot specify both 'degrees' and 'clockwise'");
                
                bool clockwise;
                if (cwObj is bool boolCw)
                    clockwise = boolCw;
                else if (bool.TryParse(cwObj.ToString(), out var parsed))
                    clockwise = parsed;
                else
                    return ToolExecutionResult.Error("Invalid clockwise value");
                
                // 90° is the square turn preset (IGridTopology.TurnStepDegrees for square).
                // Hex (60°) / triangle (120°) worlds will need this preset resolved
                // server-side from the player's cell — the tool has no world handle — so a
                // future topology-aware rotate routes the clockwise/counter-clockwise intent
                // through the gateway and lets the grain apply TurnStepDegrees. On square
                // today the constant is exactly TurnStepDegrees, so behavior is unchanged.
                degrees = clockwise ? 90 : -90;
                hasClockwise = true;
            }
            
            if (!hasDegrees && !hasClockwise)
                return ToolExecutionResult.Error("Must specify either 'degrees' or 'clockwise'");
            
            if (Math.Abs(degrees) > 360)
                return ToolExecutionResult.Error("Degrees must be between -360 and 360");
            
            // Route through the gateway so phase 2b+c can replace the in-process
            // implementation with a grain-routed one without touching this tool.
            if (context.MutationGateway != null)
            {
                var result = await context.MutationGateway.RotateAsync(degrees);
                return result.Success
                    ? ToolExecutionResult.Ok($"Rotated {degrees} degrees")
                    : ToolExecutionResult.Error(result.Reason ?? "Rotate failed");
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

