using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for opening doors or containers.
    /// </summary>
    [AgentTool("open", "Open a door or container", 
        Categories = new[] { "interaction" },
        RequiredCapabilities = new[] { "interaction" })]
    public class OpenTool : IAgentTool
    {
        public string ToolId => "open";
        public string Description => "Open a door or container by its entity ID";
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
                        Description = "Entity ID of the door or container to open"
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
            
            // Gateway FIRST — for a grain-bound session this mutates canonical state; the
            // management-grain path mutates the session-local mirror only (see MoveTool).
            if (context.MutationGateway != null)
            {
                var result = await context.MutationGateway.OpenAsync(entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Opened {entityId}")
                    : ToolExecutionResult.Error(result.Reason);
            }

            // Fallback: agent runners operating through IGameManagementGrain without a session.
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.OpenAsync(context.SessionId, entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Opened {entityId}")
                    : ToolExecutionResult.Error(result.Message);
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

