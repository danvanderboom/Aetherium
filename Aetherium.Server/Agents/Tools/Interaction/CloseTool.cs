using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for closing doors or containers.
    /// </summary>
    [AgentTool("close", "Close a door or container", 
        Categories = new[] { "interaction" },
        RequiredCapabilities = new[] { "interaction" })]
    public class CloseTool : IAgentTool
    {
        public string ToolId => "close";
        public string Description => "Close a door or container by its entity ID";
        public IEnumerable<string> Categories => new[] { "interaction" };
        public IEnumerable<string> RequiredCapabilities => new[] { "interaction" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["targetEntityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the door or container to close"
                    }
                },
                Required = new() { "targetEntityId" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("interaction"))
                return ToolExecutionResult.Error("Missing required capability: interaction");
            
            if (!args.TryGetValue("targetEntityId", out var entityIdObj))
                return ToolExecutionResult.Error("Missing required parameter: targetEntityId");
            
            var entityId = entityIdObj.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entityId))
                return ToolExecutionResult.Error("Entity ID cannot be empty");
            
            // Use management grain if available (for agent execution)
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.CloseAsync(context.SessionId, entityId);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Closed {entityId}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Route through the gateway (phase 2a: local; phase 2b+c: grain-routed).
            if (context.MutationGateway != null)
            {
                var result = await context.MutationGateway.CloseAsync(entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Closed {entityId}")
                    : ToolExecutionResult.Error(result.Reason);
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

