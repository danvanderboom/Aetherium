using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for using items on other entities (e.g., key on door).
    /// </summary>
    [AgentTool("use", "Use an item on another entity", 
        Categories = new[] { "interaction", "inventory" },
        RequiredCapabilities = new[] { "inventory_access", "interaction" })]
    public class UseTool : IAgentTool
    {
        public string ToolId => "use";
        public string Description => "Use an item from inventory on another entity (e.g., use key on door)";
        public IEnumerable<string> Categories => new[] { "interaction", "inventory" };
        public IEnumerable<string> RequiredCapabilities => new[] { "inventory_access", "interaction" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["itemEntityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the item to use (from inventory)"
                    },
                    ["onEntityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the target entity to use the item on"
                    }
                },
                Required = new() { "itemEntityId", "onEntityId" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasAllCapabilities(RequiredCapabilities))
                return ToolExecutionResult.Error("Missing required capabilities");
            
            if (!args.TryGetValue("itemEntityId", out var itemIdObj))
                return ToolExecutionResult.Error("Missing required parameter: itemEntityId");
            
            if (!args.TryGetValue("onEntityId", out var targetIdObj))
                return ToolExecutionResult.Error("Missing required parameter: onEntityId");
            
            var itemId = itemIdObj.ToString() ?? string.Empty;
            var targetId = targetIdObj.ToString() ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(itemId))
                return ToolExecutionResult.Error("Item entity ID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(targetId))
                return ToolExecutionResult.Error("Target entity ID cannot be empty");
            
            // Use management grain if available (for agent execution)
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.UseAsync(context.SessionId, itemId, targetId);
                return result.Success 
                    ? ToolExecutionResult.Ok($"Used {itemId} on {targetId}")
                    : ToolExecutionResult.Error(result.Message);
            }
            
            // Use interaction system directly (for synchronous player execution)
            if (context.InteractionSystem != null && context.Session != null)
            {
                var result = context.InteractionSystem.TryUse(context.Session, itemId, targetId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Used {itemId} on {targetId}")
                    : ToolExecutionResult.Error(result.Reason);
            }
            
            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

