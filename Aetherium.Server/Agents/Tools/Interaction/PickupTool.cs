using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for picking up items from the ground.
    /// </summary>
    [AgentTool("pickup", "Pick up an item by entity ID", 
        Categories = new[] { "interaction", "inventory" },
        RequiredCapabilities = new[] { "inventory_access" })]
    public class PickupTool : IAgentTool
    {
        public string ToolId => "pickup";
        public string Description => "Pick up an item from the ground by its entity ID";
        public IEnumerable<string> Categories => new[] { "interaction", "inventory" };
        public IEnumerable<string> RequiredCapabilities => new[] { "inventory_access" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["targetEntityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the item to pick up"
                    }
                },
                Required = new() { "targetEntityId" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("inventory_access"))
                return ToolExecutionResult.Error("Missing required capability: inventory_access");
            
            if (!args.TryGetValue("targetEntityId", out var entityIdObj))
                return ToolExecutionResult.Error("Missing required parameter: targetEntityId");
            
            var entityId = entityIdObj.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entityId))
                return ToolExecutionResult.Error("Entity ID cannot be empty");
            
            // Use management grain if available (for agent execution via IGameManagementGrain)
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.PickupAsync(context.SessionId, entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Picked up {entityId}")
                    : ToolExecutionResult.Error(result.Message);
            }

            // Route through the gateway. Phase 2a: LocalMutationGateway → InteractionSystem.
            // Phase 2b+c: GrainMutationGateway → IGameMapGrain.PickupAsync.
            if (context.MutationGateway != null)
            {
                var result = await context.MutationGateway.PickupAsync(entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Picked up {entityId}")
                    : ToolExecutionResult.Error(result.Reason);
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

