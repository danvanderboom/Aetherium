using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Core;

namespace Aetherium.Server.Agents.Tools.WorldBuilding
{
    /// <summary>
    /// Tool for spawning new entities in the world.
    /// Requires world_edit capability.
    /// </summary>
    [AgentTool("spawnentity", "Create a new entity at a specific location", 
        Categories = new[] { "worldbuilding", "entity_management" },
        RequiredCapabilities = new[] { "world_edit" })]
    public class SpawnEntityTool : IAgentTool
    {
        public string ToolId => "spawnentity";
        public string Description => "Create a new entity at specified coordinates with components";
        public IEnumerable<string> Categories => new[] { "worldbuilding", "entity_management" };
        public IEnumerable<string> RequiredCapabilities => new[] { "world_edit" };
        
        public ToolParameterSchema GetParameterSchema()
        {
            return new ToolParameterSchema
            {
                Properties = new Dictionary<string, ParameterDefinition>
                {
                    ["x"] = new()
                    {
                        Type = "number",
                        Description = "X coordinate"
                    },
                    ["y"] = new()
                    {
                        Type = "number",
                        Description = "Y coordinate"
                    },
                    ["z"] = new()
                    {
                        Type = "number",
                        Description = "Z coordinate (level)",
                        DefaultValue = 0
                    },
                    ["entityType"] = new()
                    {
                        Type = "string",
                        Description = "Type of entity to spawn (e.g., 'key', 'door', 'item')"
                    },
                    ["properties"] = new()
                    {
                        Type = "object",
                        Description = "Additional properties for the entity (JSON object)"
                    }
                },
                Required = new() { "x", "y", "entityType" }
            };
        }
        
        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context, Dictionary<string, object> args)
        {
            if (!context.HasCapability("world_edit"))
                return ToolExecutionResult.Error("Missing required capability: world_edit");
            
            // Validate parameters
            if (!args.TryGetValue("x", out var xObj) || !int.TryParse(xObj.ToString(), out var x))
                return ToolExecutionResult.Error("Invalid or missing x coordinate");
            
            if (!args.TryGetValue("y", out var yObj) || !int.TryParse(yObj.ToString(), out var y))
                return ToolExecutionResult.Error("Invalid or missing y coordinate");
            
            int z = 0;
            if (args.TryGetValue("z", out var zObj))
                int.TryParse(zObj.ToString(), out z);
            
            if (!args.TryGetValue("entityType", out var typeObj))
                return ToolExecutionResult.Error("Missing required parameter: entityType");
            
            var entityType = typeObj.ToString();
            if (string.IsNullOrWhiteSpace(entityType))
                return ToolExecutionResult.Error("Entity type cannot be empty");
            
            // TODO: Full implementation would:
            // 1. Get world/map from session or grain
            // 2. Create entity with specified type
            // 3. Add components based on entityType and properties
            // 4. Place entity at location
            // 5. Update world state
            
            // For now, return not implemented
            return ToolExecutionResult.Error("SpawnEntityTool: Full implementation pending. Would spawn " + entityType + " at (" + x + "," + y + "," + z + ")");
        }
    }
}

