using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.Interaction
{
    /// <summary>
    /// Tool for using items on other entities (e.g., key on door).
    /// Supports multi-use tools with context-gated usage options.
    /// </summary>
    [AgentTool("use", "Use an item on another entity", 
        Categories = new[] { "interaction", "inventory" },
        RequiredCapabilities = new[] { "inventory_access", "interaction" })]
    public class UseTool : IAgentTool
    {
        public string ToolId => "use";
        public string Description => "Use an item from inventory on another entity (e.g., use key on door). Supports multiple usage modes for multi-use tools.";
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
                    },
                    ["usageId"] = new()
                    {
                        Type = "string",
                        Description = "Optional usage mode ID when multiple usage options are available (e.g., 'unlock-door', 'consume', 'lockpick')"
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
            
            // Extract optional usageId
            string? usageId = null;
            if (args.TryGetValue("usageId", out var usageIdObj))
            {
                usageId = usageIdObj?.ToString();
            }
            
            // Route through the gateway (phase 2a: local; phase 2b+c: grain-routed).
            // The gateway preserves the reactive-disambiguation contract: when an
            // InteractionResultDto comes back with .Options populated, the caller
            // gets the same option-list shape.
            if (context.MutationGateway != null)
            {
                var result = await context.MutationGateway.UseAsync(itemId, targetId, usageId);

                if (result.Options != null && result.Options.Count > 0)
                {
                    var optionsData = new Dictionary<string, object>
                    {
                        ["options"] = result.Options.Select(opt => new Dictionary<string, object>
                        {
                            ["usageId"] = opt.UsageId,
                            ["label"] = opt.Label,
                            ["description"] = opt.Description
                        }).ToList()
                    };

                    return ToolExecutionResult.Ok(result.Reason, optionsData);
                }

                return result.Success
                    ? ToolExecutionResult.Ok($"Used {itemId} on {targetId}")
                    : ToolExecutionResult.Error(result.Reason);
            }

            // Use management grain if available (for agent execution).
            // Note: ManagementGrain.UseAsync doesn't support usageId; falls back to default behaviour.
            if (context.ManagementGrain != null)
            {
                var result = await context.ManagementGrain.UseAsync(context.SessionId, itemId, targetId);
                return result.Success
                    ? ToolExecutionResult.Ok($"Used {itemId} on {targetId}")
                    : ToolExecutionResult.Error(result.Message);
            }

            return ToolExecutionResult.Error("No execution context available");
        }
    }
}

