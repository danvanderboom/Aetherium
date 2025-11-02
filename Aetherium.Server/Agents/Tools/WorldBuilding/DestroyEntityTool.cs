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
            
            // Check if we have World context (WorldBuildingToolContext)
            if (context is not WorldBuildingToolContext worldContext)
                return ToolExecutionResult.Error("DestroyEntityTool requires WorldBuildingToolContext with World reference");
            
            // Check if entity exists
            if (!worldContext.World.Entities.TryGetValue(entityId, out var entity))
                return ToolExecutionResult.Error($"Entity not found: {entityId}");
            
            try
            {
                // Remove entity from world
                worldContext.World.RemoveEntity(entityId);
                return ToolExecutionResult.Ok($"Destroyed entity {entityId}");
            }
            catch (System.ArgumentException ex)
            {
                return ToolExecutionResult.Error($"Failed to destroy entity: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                return ToolExecutionResult.Error($"Failed to destroy entity: {ex.Message}");
            }
        }
    }
}

