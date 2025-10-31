using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for moving entities to new locations.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("moveentity", "Move an entity to a new location", 
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class MoveEntityTool : IAgentTool
    {
        public string ToolId => "moveentity";
        public string Description => "Move an entity to new coordinates";
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
                        Description = "Entity ID of the entity to move"
                    },
                    ["x"] = new()
                    {
                        Type = "number",
                        Description = "New X coordinate"
                    },
                    ["y"] = new()
                    {
                        Type = "number",
                        Description = "New Y coordinate"
                    },
                    ["z"] = new()
                    {
                        Type = "number",
                        Description = "New Z coordinate (level)",
                        DefaultValue = 0
                    }
                },
                Required = new() { "entityId", "x", "y" }
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
            
            if (!args.TryGetValue("x", out var xObj) || !int.TryParse(xObj.ToString(), out var x))
                return ToolExecutionResult.Error("Invalid or missing x coordinate");
            
            if (!args.TryGetValue("y", out var yObj) || !int.TryParse(yObj.ToString(), out var y))
                return ToolExecutionResult.Error("Invalid or missing y coordinate");
            
            int z = 0;
            if (args.TryGetValue("z", out var zObj))
                int.TryParse(zObj.ToString(), out z);
            
            // TODO: Full implementation would:
            // 1. Find entity in world
            // 2. Update position component
            // 3. Update spatial index
            // 4. Update world state
            
            return ToolExecutionResult.Error("MoveEntityTool: Full implementation pending. Would move entity " + entityId + " to (" + x + "," + y + "," + z + ")");
        }
    }
}

