using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for destroying entities in the world.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("destroyentity", "Remove an entity from the world", 
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class DestroyEntityTool : IAgentTool
    {
        public string ToolId => "destroyentity";
        public string Description => "Remove an entity from the world by its entity ID";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["entityId"] = new()
                    {
                        Type = "string",
                        Description = "Entity ID of the entity to destroy"
                    }
                },
                Required = new() { "entityId" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return ToolExecutionResult.Error("Missing required capability: world_edit");
            
            if (!args.TryGetValue("entityId", out var entityIdObj))
                return ToolExecutionResult.Error("Missing required parameter: entityId");
            
            var entityId = entityIdObj.ToString();
            if (string.IsNullOrWhiteSpace(entityId))
                return ToolExecutionResult.Error("Entity ID cannot be empty");
            
            // TODO: Full implementation would:
            // 1. Find entity in world
            // 2. Remove from spatial index
            // 3. Clean up references
            // 4. Destroy entity
            
            return ToolExecutionResult.Error("DestroyEntityTool: Full implementation pending. Would destroy entity " + entityId);
        }
    }
}

