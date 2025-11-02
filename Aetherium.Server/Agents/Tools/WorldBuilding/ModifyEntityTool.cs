using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for modifying entity properties and components.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("modifyentity", "Modify an entity's properties or components", 
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class ModifyEntityTool : IAgentTool
    {
        public string ToolId => "modifyentity";
        public string Description => "Modify an entity's properties or add/remove components";
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
                        Description = "Entity ID of the entity to modify"
                    },
                    ["properties"] = new()
                    {
                        Type = "object",
                        Description = "Properties to modify (JSON object)"
                    },
                    ["addComponents"] = new()
                    {
                        Type = "array",
                        Description = "Components to add (array of component names)"
                    },
                    ["removeComponents"] = new()
                    {
                        Type = "array",
                        Description = "Components to remove (array of component names)"
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
                return ToolExecutionResult.Error("ModifyEntityTool requires WorldBuildingToolContext with World reference");
            
            // Find entity in world
            if (!worldContext.World.Entities.TryGetValue(entityId, out var entity))
                return ToolExecutionResult.Error($"Entity not found: {entityId}");
            
            // TODO: Full implementation would:
            // 1. Apply property modifications from args["properties"]
            // 2. Add components from args["addComponents"]
            // 3. Remove components from args["removeComponents"]
            // These operations require component system knowledge and reflection
            
            // For now, return not implemented since component modification requires
            // detailed knowledge of component system and reflection
            return ToolExecutionResult.Error($"ModifyEntityTool: Component modification requires component system knowledge. Would modify entity {entityId}");
        }
    }
}

