using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for dropping items from inventory.
    /// </summary>
    [AgentTool("drop", "Drop an item from inventory", 
        Categories = new[] { "interaction", "inventory" },
        RequiredCapabilities = new[] { "inventory_access" })]
    public class DropTool : IAgentTool
    {
        public string ToolId => "drop";
        public string Description => "Drop an item from inventory by its entity ID";
        public IEnumerable<string> Categories => new[] { "interaction", "inventory" };
        public IEnumerable<string> RequiredCapabilities => new[] { "inventory_access" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["itemEntityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the item to drop from inventory"
                    }
                },
                Required = new() { "itemEntityId" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("inventory_access"))
                return ToolExecutionResult.Error("Missing required capability: inventory_access");
            
            if (!args.TryGetValue("itemEntityId", out var entityIdObj))
                return ToolExecutionResult.Error("Missing required parameter: itemEntityId");
            
            var entityId = entityIdObj.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entityId))
                return ToolExecutionResult.Error("Item entity ID cannot be empty");
            
            // Use management grain if available (for agent execution)
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.DropAsync(context.SessionId, entityId);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Dropped {entityId}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use interaction system directly (for synchronous player execution)
            if (context.InteractionSystem != null && context.Session != null)
            {
                var result = context.InteractionSystem.TryDrop(context.Session, entityId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Dropped {entityId}")
                    : ToolExecutionResult.Error(result.Reason);
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

